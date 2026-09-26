#!/usr/bin/env python3
"""GK2 Vanilla+ – lokales Workshop-Dashboard.

Fragt alle paar Minuten die oeffentliche Steam-API ab (kein API-Key noetig), speichert den Verlauf
in data/history.jsonl und zeigt ihn unter http://localhost:8765 an. Nur Python-Standardbibliothek.
Start: python3 server.py   (oder Dashboard-starten.command doppelklicken)
"""
import json
import os
import re
import threading
import time
import urllib.parse
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ITEM_ID = "3808053878"
PORT = 8765
POLL_SECONDS = 300          # alle 5 Minuten
ROOT = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(ROOT, "data")
HISTORY = os.path.join(DATA, "history.jsonl")
UA = {"User-Agent": "GK2VanillaPlus-Dashboard/1.0"}
PAGE_UA = {"User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15",
           "Accept-Language": "en"}
PAGE_SECONDS = 1800         # Workshop-Seite (Kommentare, Bewertung) nur alle 30 Minuten, sonst sperrt Steam kurz (429)

lock = threading.Lock()
state = {"last_ok": None, "last_error": None, "next_poll": None, "meta": {}, "page": {}, "page_at": 0}


def fetch_api():
    body = urllib.parse.urlencode({"itemcount": 1, "publishedfileids[0]": ITEM_ID}).encode()
    req = urllib.request.Request(
        "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", data=body, headers=UA)
    with urllib.request.urlopen(req, timeout=20) as r:
        d = json.load(r)["response"]["publishedfiledetails"][0]
    return d


def fetch_page():
    """Kommentare, Diskussionen und Bewertung stehen nur auf der Workshop-Seite."""
    req = urllib.request.Request(
        "https://steamcommunity.com/sharedfiles/filedetails/?id=" + ITEM_ID + "&l=english", headers=PAGE_UA)
    with urllib.request.urlopen(req, timeout=20) as r:
        html = r.read().decode("utf-8", "ignore")
    out = {}
    m = re.search(r"Comments<span class=\"tabCount\">([\d,]+)", html)
    if m:
        out["comments"] = int(m.group(1).replace(",", ""))
    m = re.search(r"Discussions<span class=\"tabCount\">([\d,]+)", html)
    if m:
        out["discussions"] = int(m.group(1).replace(",", ""))
    m = re.search(r"sharedfiles/(\d)-star_large\.png", html)
    out["stars"] = int(m.group(1)) if m else None
    m = re.search(r"([\d,]+) ratings", html)
    out["ratings"] = int(m.group(1).replace(",", "")) if m else 0
    m = re.search(r"(\d+) Change Notes", html)
    if m:
        out["change_notes"] = int(m.group(1))
    return out


def poll_once():
    d = fetch_api()
    snap = {
        "t": int(time.time()),
        "subs": int(d.get("subscriptions", 0)),
        "subs_total": int(d.get("lifetime_subscriptions", 0)),
        "favs": int(d.get("favorited", 0)),
        "favs_total": int(d.get("lifetime_favorited", 0)),
        "views": int(d.get("views", 0)),
        "size": int(d.get("file_size", 0) or 0),
    }
    page_err = None
    if time.time() - state["page_at"] >= PAGE_SECONDS or not state["page"]:
        try:
            state["page"] = fetch_page()
            state["page_at"] = time.time()
        except Exception as e:  # Seite optional, API reicht - naechster Versuch in 30 min
            state["page_at"] = time.time()
            page_err = "Workshop-Seite: " + str(e)
    snap.update(state["page"])
    meta = {
        "title": d.get("title"),
        "preview": d.get("preview_url"),
        "created": d.get("time_created"),
        "updated": d.get("time_updated"),
        "tags": [t.get("tag") for t in d.get("tags", [])],
        "visibility": d.get("visibility"),
        "banned": d.get("banned"),
        "url": "https://steamcommunity.com/sharedfiles/filedetails/?id=" + ITEM_ID,
    }
    with lock:
        os.makedirs(DATA, exist_ok=True)
        with open(HISTORY, "a", encoding="utf-8") as f:
            f.write(json.dumps(snap) + "\n")
        state["meta"] = meta
        state["last_ok"] = snap["t"]
        state["page_error"] = page_err
    return snap


def poller():
    while True:
        try:
            poll_once()
            state["last_error"] = None
        except Exception as e:
            state["last_error"] = str(e)
        state["next_poll"] = int(time.time()) + POLL_SECONDS
        time.sleep(POLL_SECONDS)


def read_history():
    rows = []
    if os.path.exists(HISTORY):
        with lock, open(HISTORY, encoding="utf-8") as f:
            for line in f:
                try:
                    rows.append(json.loads(line))
                except ValueError:
                    pass
    return rows


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send(self, code, body, ctype):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/data":
            body = json.dumps({"history": read_history(), "meta": state["meta"], "last_ok": state["last_ok"],
                               "last_error": state["last_error"] or state.get("page_error"), "next_poll": state["next_poll"],
                               "poll_seconds": POLL_SECONDS, "now": int(time.time())}).encode()
            return self.send(200, body, "application/json")
        if path == "/api/refresh":
            try:
                poll_once()
                state["last_error"] = None
            except Exception as e:
                state["last_error"] = str(e)
            return self.send(200, b'{"ok":true}', "application/json")
        if path in ("/", "/index.html"):
            with open(os.path.join(ROOT, "index.html"), "rb") as f:
                return self.send(200, f.read(), "text/html; charset=utf-8")
        self.send(404, b"not found", "text/plain")


def restore_page_values():
    rows = read_history()
    for r in reversed(rows):
        if "comments" in r:
            state["page"] = {k: r[k] for k in ("comments", "discussions", "stars", "ratings", "change_notes") if k in r}
            break


if __name__ == "__main__":
    restore_page_values()
    threading.Thread(target=poller, daemon=True).start()
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print("GK2 Vanilla+ Dashboard: http://localhost:%d  (Strg+C zum Beenden)" % PORT)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
