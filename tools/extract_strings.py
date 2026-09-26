#!/usr/bin/env python3
"""Sammelt alle englischen Texte des Mod-Menues (Schluessel der Sprachdateien) und
schreibt lang/_template.txt. Meldet fehlende Eintraege in lang/xx.txt (-v = auflisten).
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "GK2Tweaks")
LANG = os.path.join(ROOT, "lang")
LIT = r'"((?:[^"\\]|\\.)*)"'


def unesc(s):
    return s.replace('\\"', '"').replace("\\\\", "\\")


def strip_dev(text):
    return re.sub(r"#if DEV.*?#endif", "", text, flags=re.S)


def collect():
    keys, seen = [], set()

    def add(s):
        s = unesc(s).strip()
        if not s or s in seen or not re.search(r"[A-Za-z]", s):
            return
        seen.add(s)
        keys.append(s)

    for fn in sorted(os.listdir(SRC)):
        if not fn.endswith(".cs") or fn in ("Benchmark.cs", "Profiling.cs", "UiDump.cs"):
            continue
        text = strip_dev(open(os.path.join(SRC, fn), encoding="utf-8").read())
        for m in re.finditer(r"\bT\(\s*" + LIT + r"\s*,\s*" + LIT + r"\s*\)", text):
            add(m.group(2))
        for m in re.finditer(r"new\[\]\s*\{\s*" + LIT + r"\s*,\s*" + LIT + r"\s*\}", text):
            add(m.group(2))
        for m in re.finditer(r"\bEn\s*=\s*" + LIT, text):
            add(m.group(1))
        if fn == "Plugin.cs":
            for m in re.finditer(r"Config\.Bind\(\s*" + LIT + r"\s*,\s*" + LIT + r"\s*,[^;]*?,\s*(?:new ConfigDescription\(\s*)?" + LIT, text, flags=re.S):
                if m.group(1) == "Benchmark":
                    continue
                add(unesc(m.group(3)).split(" / ")[0])
    return keys


def read_lang(path):
    d = {}
    if not os.path.exists(path):
        return d
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line or line.startswith("#") or " => " not in line:
            continue
        k, v = line.split(" => ", 1)
        if v.strip():
            d[k.strip()] = v.strip()
    return d


def main():
    keys = collect()
    os.makedirs(LANG, exist_ok=True)
    with open(os.path.join(LANG, "_template.txt"), "w", encoding="utf-8") as f:
        f.write("# GK2 Vanilla+ - translation template\n")
        f.write("# Copy this file to <language code>.txt (e.g. it.txt, pl.txt, pt.txt), put it into\n")
        f.write("# <game folder>/BepInEx/GK2VanillaPlus/lang/ and write the translation after \" => \".\n")
        f.write("# Keep numbers, key names and symbols as they are. \\n = line break.\n")
        f.write("# The mod uses the file when the game is set to that language. Send finished files to the author (GitHub / Workshop comments).\n\n")
        for k in keys:
            f.write(k.replace("\n", "\\n") + " => \n")
    print(len(keys), "keys")
    for fn in sorted(os.listdir(LANG)):
        if fn.startswith("_") or not fn.endswith(".txt"):
            continue
        d = read_lang(os.path.join(LANG, fn))
        missing = [k for k in keys if k.replace("\n", "\\n") not in d]
        print(fn, "missing", len(missing))
        if "-v" in sys.argv:
            for k in missing:
                print("   ", k)


if __name__ == "__main__":
    main()
