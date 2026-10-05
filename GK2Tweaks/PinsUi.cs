using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GK2Tweaks
{
    // Pin-Liste im Stil des Spiels: Rahmen wie die Tooltips des Spiels, je Pin eine Karte mit Kopfleiste
    // (Symbol, Name, Fortschritt), Fortschrittsbalken, Zutaten mit rechtsbuendigen Zahlen und Baum-Linien.
    internal sealed partial class TweaksGui
    {
        private GUIStyle pnPanel, pnHead, pnTitle, pnName, pnCount, pnInfo, pnBtn, pnProg;
        private string pnKey;
        private static Texture2D pnWhite;
        private float pnRetry;
        private int pnFs = 16;

        private void EnsurePinUi()
        {
            EnsurePinStyles();
            string key = pinStyleKey + skinned;
            if (pnPanel != null && pnKey == key) return;
            pnKey = key;
            bool hc = Plugin.HighContrast.Value;
            int f = pinRowStyle.fontSize;
            if (pnWhite == null) { pnWhite = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; pnWhite.SetPixel(0, 0, Color.white); pnWhite.Apply(); }

            pnPanel = new GUIStyle(pinBoxStyle);
            // dunkle Schiefer-Platte des Spiels (wie die Kopfzeilen im Spiel-Menue), Kopfleiste nur als dunklerer Streifen
            if (skinned && !hc) { try { pnPanel = GameSkin.Box("craft_window-craft_plate", GUIStyle.none, f, Color.white); } catch { } }
            pnHead = null;

            pnTitle = new GUIStyle(pinTitleStyle) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            pnName = new GUIStyle(pinRowStyle) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            pnName.normal.textColor = hc ? Color.white : new Color(0.93f, 0.89f, 0.8f);
            pnCount = new GUIStyle(pinRowStyle) { alignment = TextAnchor.MiddleRight };
            pnInfo = new GUIStyle(pinInfoStyle) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Italic, clipping = TextClipping.Clip, richText = true };
            pnInfo.normal.textColor = hc ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.8f, 0.77f, 0.7f);
            pnProg = new GUIStyle(pinInfoStyle) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.Max(11, f - 2) };
            pnBtn = new GUIStyle(skinned ? arrowStyle : buttonStyle) { fontSize = Mathf.Max(12, f - 1), alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 1), fixedHeight = 0, fixedWidth = 0, margin = new RectOffset(0, 0, 0, 0) };
        }

        // "×4" in Werkbank-Zeilen hervorheben
        private static string Hi(string s) => s == null ? null : Regex.Replace(s, @"×\d+", m => "<b><color=#f0c85a>" + m.Value + "</color></b>");

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, pnWhite);
            GUI.color = old;
        }

        // erfuellter Anteil (0..1) und Anzahl fertiger Zutaten
        private static float Progress(Pins.Pin p, out int ok, out int all)
        {
            ok = 0; all = 0;
            float got = 0f, need = 0f;
            foreach (Pins.Need n in p.Needs)
            {
                all++;
                if (n.Have >= n.Count) ok++;
                got += Mathf.Min(n.Have, n.Count);
                need += Mathf.Max(1, n.Count);
            }
            if (p.QuestId != null) return p.Ready ? 1f : 0f;
            return need > 0f ? got / need : (p.Ready ? 1f : 0f);
        }

        private void DrawPins(float scale, Rect ov)
        {
            EnsurePinUi();
            PinPadBegin();
            float f = pinRowStyle.fontSize;
            pnFs = Mathf.RoundToInt(f);
            float line = f + 11f, icon = f + 5f, head = f + 18f, pad = 12f, indent = f + 4f, bs = f + 7f;
            string readyTxt = Labels.T("bereit", "ready"), doneTxt = Labels.T("erledigt", "done");
            Color ok = Plugin.HighContrast.Value ? new Color(0.49f, 1f, 0.36f) : new Color(0.61f, 0.89f, 0.5f);
            Color bad = Plugin.HighContrast.Value ? new Color(1f, 0.3f, 0.24f) : new Color(1f, 0.48f, 0.42f);
            Color guide = new Color(1f, 0.9f, 0.7f, 0.16f);

            // ---- Groesse ----
            float w = 0f, h = 0f, countW = pnCount.CalcSize(new GUIContent("999 / 999")).x;
            foreach (Pins.Pin p in Pins.List)
            {
                string st = p.Ready ? "  " + (p.QuestId != null ? doneTxt : readyTxt) : "";
                w = Mathf.Max(w, bs + 6f + icon + 8f + pnTitle.CalcSize(new GUIContent(p.Title + st)).x + 12f + pnProg.CalcSize(new GUIContent("99/99")).x + 8f + bs + pad + bs + 30f);
                h += head + 6f;
                if (p.Collapsed) { h += 6f; continue; }
                string hd = PinHeader(p);
                if (hd != null) { w = Mathf.Max(w, pnInfo.CalcSize(new GUIContent(hd)).x + 30f + (p.VarCount > 1 ? 2f * bs + 12f : 0f)); h += line; }
                if (p.Rows.Count > 0)
                    foreach (PinRow r in p.Rows)
                    {
                        float nw = (r.Info ? pnInfo : pnName).CalcSize(new GUIContent(r.Name)).x;
                        w = Mathf.Max(w, 14f + indent * r.Depth + bs + 4f + icon + 6f + nw + (r.Info ? 8f : 18f + countW));
                        h += line;
                    }
                else
                    foreach (Pins.Need n in p.Needs)
                    {
                        w = Mathf.Max(w, 14f + icon + 6f + pnName.CalcSize(new GUIContent(n.Name)).x + 18f + countW);
                        h += line;
                    }
                if (p.FuelName != null) { w = Mathf.Max(w, 14f + bs + 4f + icon + 6f + pnInfo.CalcSize(new GUIContent(p.FuelName + "  ×" + p.FuelCount)).x); h += line; }
                if (!string.IsNullOrEmpty(p.Note)) w = Mathf.Max(w, Mathf.Min(f * 20f, pinNoteStyle.CalcSize(new GUIContent(p.Note)).x + 30f));
                h += 10f;
            }
            w = Mathf.Max(w + 2f * pad, 240f);
            foreach (Pins.Pin p in Pins.List)
                if (!p.Collapsed && !string.IsNullOrEmpty(p.Note)) h += Mathf.Min(pinNoteStyle.CalcHeight(new GUIContent(p.Note), w - 2f * pad - 16f), pinNoteStyle.lineHeight * 4f + 4f);
            h += 2f * pad - 4f;

            // ---- Position (wie bisher) ----
            float sw = Screen.width / scale, sh = Screen.height / scale, m = 12f;
            string c = Plugin.PinsCorner.Value;
            float ins = GK2Tweaks.HudCenter.Inset(scale);
            float x = c.EndsWith("Right") ? sw - w - m - ins : m + ins;
            float y;
            if (c.StartsWith("Top")) y = ov.height > 0 ? ov.yMax + 6f : TopFor(c);
            else y = ov.height > 0 ? ov.y - 6f - h : sh - h - m;
            Rect npc = NpcWidgetRect(scale);
            if (npc.width > 0 && npc.Overlaps(new Rect(x, y, w, h)))
                y = c.StartsWith("Top") ? npc.yMax + 6f : npc.y - 6f - h;
            if (y + h > sh - m && c.StartsWith("Top")) h = Mathf.Max(line * 3f, sh - m - y);

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, pnPanel);
            float cx = x + pad, cw = w - 2f * pad, cy = y + pad - 2f, bottom = y + h - pad + 4f;
            Pins.Pin remove = null;
            Action act = null;
            bool first = true;
            foreach (Pins.Pin p in Pins.List)
            {
                if (cy + head > bottom + 1f) break;
                Pins.Pin pp = p;
                if (!first) { Fill(new Rect(cx + 4f, cy - 5f, cw - 8f, 1f), guide); }
                first = false;

                // Kopfleiste
                var hr = new Rect(cx, cy, cw, head);
                Fill(hr, new Color(0f, 0f, 0f, 0.28f));
                Fill(new Rect(hr.x, hr.yMax - 1f, hr.width, 1f), new Color(1f, 0.86f, 0.55f, 0.25f));
                float prog = Progress(p, out int nOk, out int nAll);
                if (PinFocusHere(hr))
                {
                    if (TakeCmd(ref pinCmdA)) act = () => PinTree.ToggleCollapsed(pp);
                    int lr = TakeLR();
                    if (lr != 0 && p.QuestId == null && p.Needs.Count > 0 && !IsOrder(p)) act = () => PinTree.StepMult(pp, lr);
                    if (TakeCmd(ref pinCmdLB) && p.VarCount > 1) act = () => PinTree.Switch(pp, -1);
                    if (TakeCmd(ref pinCmdRB) && p.VarCount > 1) act = () => PinTree.Switch(pp, 1);
                    if (TakeCmd(ref pinCmdY)) remove = p;
                }
                if (KitButton(new Rect(hr.x + 5f, hr.y + (head - bs) / 2f, bs, bs), p.Collapsed ? "+" : "-", p.Collapsed ? Labels.T("Aufklappen", "Expand") : Labels.T("Einklappen", "Collapse"), KitGrey, pnFs))
                    act = () => PinTree.ToggleCollapsed(pp);
                float tx = hr.x + 5f + bs + 7f;
                Texture2D ti = Pins.Icon(p.IconId);
                if (ti != null) { GUI.DrawTexture(new Rect(tx, hr.y + (head - icon) / 2f, icon, icon), ti, ScaleMode.ScaleToFit); tx += icon + 7f; }
                float rightW = bs + 6f;
                // Menge: wie oft das Rezept hergestellt werden soll (Klick = mehr, Rechtsklick = weniger)
                if (p.QuestId == null && p.Needs.Count > 0 && !IsOrder(p))
                {
                    string mt = "×" + Math.Max(1, p.Mult);
                    float mw = Mathf.Max(bs, pnBtn.CalcSize(new GUIContent(mt)).x + 10f);
                    var mr = new Rect(hr.xMax - rightW - mw - 2f, hr.y + (head - bs) / 2f, mw, bs);
                    bool right = Event.current.type == EventType.MouseUp && Event.current.button == 1 && mr.Contains(Event.current.mousePosition);
                    if (right) { Event.current.Use(); act = () => PinTree.StepMult(pp, -1); }
                    if (KitButton(mr, mt, Labels.T("Wie oft herstellen? Klick = mehr, Rechtsklick = weniger", "How many times to craft? Click = more, right click = less"), KitGrey, pnFs)) act = () => PinTree.StepMult(pp, 1);
                    rightW += mw + 4f;
                }
                string progTxt = p.QuestId == null && nAll > 0 ? nOk + "/" + nAll : "";
                float pw = progTxt.Length > 0 ? pnProg.CalcSize(new GUIContent(progTxt)).x + 8f : 0f;
                string status = p.Ready ? "  <color=#" + ColorUtility.ToHtmlStringRGB(ok) + ">" + (p.QuestId != null ? doneTxt : readyTxt) + "</color>" : "";
                GUI.Label(new Rect(tx, hr.y, hr.xMax - tx - rightW - pw - 4f, head), p.Title + status, pnTitle);
                if (pw > 0) GUI.Label(new Rect(hr.xMax - rightW - pw - 2f, hr.y, pw, head), progTxt, pnProg);
                if (KitButton(new Rect(hr.xMax - bs - 5f, hr.y + (head - bs) / 2f, bs, bs), "x", Labels.T("Loslösen", "Unpin"), KitRed, pnFs)) remove = p;
                // Fortschrittsbalken unter der Kopfleiste
                var bar = new Rect(hr.x + 6f, hr.yMax + 1f, hr.width - 12f, 3f);
                Fill(bar, new Color(0f, 0f, 0f, 0.45f));
                if (prog > 0f) Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(prog), bar.height), prog >= 1f ? ok : new Color(0.91f, 0.77f, 0.42f));
                cy += head + 6f;
                if (p.Collapsed) { cy += 6f; continue; }

                if (!string.IsNullOrEmpty(p.Note))
                {
                    float nh = Mathf.Min(pinNoteStyle.CalcHeight(new GUIContent(p.Note), cw - 16f), pinNoteStyle.lineHeight * 4f + 4f);
                    GUI.Label(new Rect(cx + 8f, cy, cw - 16f, nh), p.Note, pinNoteStyle);
                    cy += nh;
                }
                string hd = PinHeader(p);
                if (hd != null && cy + line <= bottom + 1f)
                {
                    float hw = cw - 16f;
                    if (p.VarCount > 1)
                    {
                        hw -= 2f * bs + 10f;
                        if (KitButton(new Rect(cx + cw - 2f * bs - 8f, cy + (line - bs) / 2f, bs, bs), "<", Labels.T("Andere Rezept-Variante", "Other recipe variant"), KitGrey, pnFs)) act = () => PinTree.Switch(pp, -1);
                        if (KitButton(new Rect(cx + cw - bs - 4f, cy + (line - bs) / 2f, bs, bs), ">", Labels.T("Andere Rezept-Variante", "Other recipe variant"), KitGrey, pnFs)) act = () => PinTree.Switch(pp, 1);
                    }
                    GUI.Label(new Rect(cx + 8f, cy, hw, line), Hi(hd), pnInfo);
                    cy += line;
                }

                float countX = cx + cw - countW - 4f;
                if (p.Rows.Count > 0)
                {
                    foreach (PinRow r in p.Rows)
                    {
                        if (cy + line > bottom + 1f) break;
                        float rx = cx + 8f + indent * r.Depth;
                        // Baum-Linie fuer tiefere Ebenen
                        for (int d = 1; d <= r.Depth; d++) Fill(new Rect(cx + 8f + indent * d - indent / 2f + bs / 2f - 4f, cy, 1f, line), guide);
                        if (r.Info)
                        {
                            GUI.Label(new Rect(rx + bs + 4f, cy, countX - rx - bs, line), Hi(r.Name), pnInfo);
                            cy += line;
                            continue;
                        }
                        if (r.Expandable && PinFocusHere(new Rect(rx, cy, cx + cw - rx, line)) && TakeCmd(ref pinCmdA))
                        {
                            string fpath = r.Path;
                            act = () => PinTree.ToggleOpen(pp, fpath);
                        }
                        if (r.Expandable)
                        {
                            string path = r.Path;
                            if (KitButton(new Rect(rx, cy + (line - bs) / 2f, bs, bs), r.Expanded ? "-" : "+", r.Expanded ? Labels.T("Zutaten zuklappen", "Hide ingredients") : Labels.T("Zutaten dafür zeigen", "Show its ingredients"), KitGrey, pnFs))
                                act = () => PinTree.ToggleOpen(pp, path);
                        }
                        float ix = rx + bs + 4f;
                        Texture2D ni = Pins.Icon(r.IconId);
                        if (ni != null) GUI.DrawTexture(new Rect(ix, cy + (line - icon) / 2f, icon, icon), ni, ScaleMode.ScaleToFit);
                        bool done = r.Have >= r.Count;
                        Color nc = pnName.normal.textColor;
                        if (done) pnName.normal.textColor = new Color(nc.r, nc.g, nc.b, 0.6f);
                        GUI.Label(new Rect(ix + icon + 6f, cy, countX - ix - icon - 10f, line), r.Name, pnName);
                        pnName.normal.textColor = nc;
                        pnCount.normal.textColor = done ? ok : bad;
                        GUI.Label(new Rect(countX, cy, countW, line), r.Have + " / " + r.Count, pnCount);
                        cy += line;
                    }
                }
                else
                {
                    foreach (Pins.Need n in p.Needs)
                    {
                        if (cy + line > bottom + 1f) break;
                        float ix = cx + 8f;
                        Texture2D ni = Pins.Icon(n.IconId);
                        if (ni != null) GUI.DrawTexture(new Rect(ix, cy + (line - icon) / 2f, icon, icon), ni, ScaleMode.ScaleToFit);
                        bool done = n.Have >= n.Count;
                        Color nc = pnName.normal.textColor;
                        if (done) pnName.normal.textColor = new Color(nc.r, nc.g, nc.b, 0.6f);
                        GUI.Label(new Rect(ix + icon + 6f, cy, countX - ix - icon - 10f, line), n.Name, pnName);
                        pnName.normal.textColor = nc;
                        pnCount.normal.textColor = done ? ok : bad;
                        GUI.Label(new Rect(countX, cy, countW, line), n.Have + " / " + n.Count, pnCount);
                        cy += line;
                    }
                }
                if (p.FuelName != null && cy + line <= bottom + 1f)
                {
                    float fx = cx + 8f + bs + 4f;
                    Texture2D fi = Pins.Icon(p.FuelIcon);
                    if (fi != null) GUI.DrawTexture(new Rect(fx, cy + (line - icon) / 2f, icon, icon), fi, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(fx + icon + 6f, cy, cw - (fx - cx) - icon - 10f, line), Hi(p.FuelName + "  ×" + p.FuelCount), pnInfo);
                    cy += line;
                }
                cy += 10f;
            }
            PinPadEnd(new Rect(x, y, w, h));
            if (remove != null) { Pins.Pin r = remove; Defer(() => Pins.Unpin(r)); }
            else if (act != null) Defer(act);
        }

        private static bool IsOrder(Pins.Pin p) => p.Key != null && p.Key.StartsWith(OrderPins.Prefix);
    }
}
