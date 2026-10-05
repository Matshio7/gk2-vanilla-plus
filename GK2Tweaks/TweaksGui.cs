using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2Tweaks
{
    // Mod-Menue (F9) und FPS-Anzeige (F10). Zeigt alle Einstellungen dieses Mods und aller anderen BepInEx-Mods.
    internal sealed partial class TweaksGui : MonoBehaviour
    {
        private const int WindowId = 0x6B2D, WeekWindowId = 0x6B2E, NewsWindowId = 0x6B2F, RateWindowId = 0x6B30;
        private bool newsOpen, newsSinceUpdate;
        private Rect newsWin;
        private Vector2 newsScroll;
        private GUIStyle newsStyle;
#if !NEXUS
        private bool rateOpen;
        private Rect rateWin;

        internal void ShowRatePrompt()
        {
            rateOpen = true;
            rateWin = new Rect(0, 0, 0, 0);
            UpdateEnabled();
        }

        private void CloseRate(int laterSessions)
        {
            rateOpen = false;
            if (laterSessions > 0) Plugin.RatePromptNextAt.Value = Plugin.RatePromptSessions.Value + laterSessions;
            else Plugin.RatePromptDone.Value = true;
            UpdateEnabled();
        }
#endif

        internal void ShowNews(bool sinceUpdate)
        {
            newsOpen = true;
            newsSinceUpdate = sinceUpdate;
            newsScroll = Vector2.zero;
            newsWin = new Rect(0, 0, 0, 0);
            UpdateEnabled();
        }

        private void CloseNews()
        {
            newsOpen = false;
            Changelog.MarkSeen();
            UpdateEnabled();
        }
        private bool menuOpen, weekOpen;
        private Rect weekWin = new Rect(0, 0, 0, 0);
        private string backupMsg = "";
        private Backups.Item confirmRestore;
        private float backupListAt;
        private System.Collections.Generic.List<Backups.Item> backupList = new System.Collections.Generic.List<Backups.Item>();

        internal void ToggleWeekPlan()
        {
            weekOpen = !weekOpen && WeekPlan.InGame;
            UpdateEnabled();
        }
        private Rect win = new Rect(40, 60, 700, 760);
        private Vector2 scroll;
        private readonly FrameStats stats = new FrameStats();
        private float statsNext;
        private string fpsText = "...";
        private string overlayText = "...";
        private GUIStyle overlayStyle, labelStyle, smallStyle, headerStyle, valueStyle, buttonStyle, windowStyle, arrowStyle, titleStyle, frameStyle, vbarStyle, vthumbStyle;
        private bool skinned;
        private Texture2D darkTex;
        private readonly Dictionary<ConfigEntryBase, string> textBuffers = new Dictionary<ConfigEntryBase, string>();
        private ConfigEntry<KeyboardShortcut> capturingKey;

        internal bool MenuOpen => menuOpen;
        internal bool CapturingKey => capturingKey != null;
        internal void ScrollToEnd() => scroll.y = 100000f;

        internal void ToggleMenu() => SetMenu(!menuOpen);

        // Wird beim Schliessen einmal aufgerufen (z. B. um das Haupt- oder Pausenmenue des Spiels wieder zu oeffnen)
        internal Action OnMenuClosed;

        private Action pendingReopen;
        private int reopenFrame;

        // Klicks, die Fenster oeffnen/schliessen oder die Anzahl an GUILayout-Controls in einem Fenster
        // aendern, duerfen den Zustand NICHT sofort mitten in OnGUI aendern (Layout- und Repaint-Durchlauf
        // koennten dann unterschiedlich viele Controls sehen -> Unity-Fehler "controls when doing repaint").
        // Stattdessen wird die eigentliche Aenderung hier gesammelt und einmal pro Frame in Tick() (also
        // ausserhalb von OnGUI) ausgefuehrt.
        private Action pendingWindowAction;
        private void Defer(Action a) => pendingWindowAction = a;

        // erst im naechsten Frame, damit ein Esc-Druck nicht gleich das wieder geoeffnete Spielmenue schliesst
        private void RunPendingReopen()
        {
            if (pendingReopen == null || Time.frameCount < reopenFrame) return;
            Action a = pendingReopen;
            pendingReopen = null;
            try { a(); } catch (Exception e) { Plugin.Log.LogWarning("Menu reopen: " + e.Message); }
        }

        internal void OpenFromGameMenu(Action reopen)
        {
            SetMenu(true);
            OnMenuClosed = reopen;
        }

        internal void SetMenu(bool open)
        {
            if (menuOpen == open) return;
            menuOpen = open;
            capturingKey = null;
            // Spielersteuerung sperren, damit Klicks im Menue nicht im Spiel landen
            try { MainGame.PlayerController?.SetControlTakenType(TakenControlType.ByTeleport, !open); } catch { }
            UpdateEnabled();
            if (!open && OnMenuClosed != null) { pendingReopen = OnMenuClosed; reopenFrame = Time.frameCount + 1; OnMenuClosed = null; }
        }

        internal void Tick(float dt)
        {
            if (pendingWindowAction != null)
            {
                Action a = pendingWindowAction;
                pendingWindowAction = null;
                try { a(); } catch (Exception e) { Plugin.Log.LogWarning("Window action: " + e.Message); }
            }
            updateAvailableSnap = UpdateCheck.Available;
            safeNoticeSnap = SafeMode.MenuNotice();
            InGameTick();
            PinPadTick();
            PadTick();
            RunPendingReopen();
            // Esc schliesst immer das oberste Fenster (gleiche Reihenfolge wie B am Controller)
            if (capturingKey == null && Input.GetKeyDown(KeyCode.Escape))
            {
#if !NEXUS
                if (rateOpen) CloseRate(6);
                else
#endif
                if (celebOpen) CloseCeleb();
                else if (newsOpen) CloseNews();
                else if (menuOpen) SetMenu(false);
            }
            UpdateEnabled();
            OverlayStats.Configure(Plugin.ShowOverlay.Value, Plugin.OvCpu.Value, Plugin.OvGpu.Value, Plugin.OvRam.Value, Plugin.OvVram.Value, Plugin.OvFrameTime.Value, Plugin.OvGpuTemp.Value);
            if (!enabled) return;
            stats.Add(dt);
            float now = Time.realtimeSinceStartup;
            if (now < statsNext) return;
            if (stats.Count > 0)
            {
                FrameResult r = stats.Compute();
                fpsText = $"{r.AvgFps:0} FPS   1%: {r.Low1Fps:0}   max {r.MaxMs:0} ms";
                overlayText = BuildOverlay(r);
            }
            stats.Reset();
            statsNext = now + 0.5f;
        }

        private static string BuildOverlay(FrameResult r)
        {
            var val = new Dictionary<string, string>();
            const float GB = 1024f * 1024f * 1024f;
            if (Plugin.OvFps.Value) val["Fps"] = $"{r.AvgFps:0} FPS";
            if (Plugin.OvLows.Value) val["Lows"] = $"1%: {r.Low1Fps:0}";
            if (Plugin.OvFrameTime.Value) val["FrameTime"] = r.AvgFps > 0 ? $"{1000f / r.AvgFps:0.0} ms (max {r.MaxMs:0})" : "- ms";
            if (Plugin.OvCpu.Value) val["Cpu"] = OverlayStats.CpuPercent >= 0 ? $"CPU {OverlayStats.CpuPercent:0} %" : "CPU …";
            if (Plugin.OvGpu.Value)
            {
                if (OverlayStats.GpuPercent >= 0) val["Gpu"] = $"GPU {OverlayStats.GpuPercent:0} %";
                else if (OverlayStats.GpuFrameMs > 0) val["Gpu"] = $"GPU {OverlayStats.GpuFrameMs:0.0} ms";
                else val["Gpu"] = "GPU n/a";
            }
            if (Plugin.OvGpuTemp.Value) val["GpuTemp"] = OverlayStats.GpuTemp > 0 ? $"GPU {OverlayStats.GpuTemp:0} °C" : "GPU °C n/a";
            if (Plugin.OvRam.Value)
            {
                long ram = OverlayStats.RamUsed;
                val["Ram"] = ram > 0 ? $"RAM {ram / GB:0.0}/{SystemInfo.systemMemorySize / 1024f:0} GB" : "RAM …";
            }
            if (Plugin.OvVram.Value)
            {
                long v = OverlayStats.VramBytes;
                long budget = System.Threading.Interlocked.Read(ref OverlayStats.VramBudget);
                int total = SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize : (int)(budget / (1024 * 1024));
                val["Vram"] = v > 0 ? (total > 0 ? $"VRAM {v / GB:0.0}/{total / 1024f:0} GB" : $"VRAM {v / GB:0.0} GB") : "VRAM n/a";
            }
            if (Plugin.OvResolution.Value) val["Resolution"] = $"{Screen.width}x{Screen.height}";
            if (Plugin.OvClock.Value) val["Clock"] = DateTime.Now.ToString("HH:mm");
            if ((Plugin.OvWeekday.Value || Plugin.OvGameTime.Value) && WeekPlan.InGame)
            {
                try
                {
                    var env = MainGame.Instance.GameSave.environmentData;
                    if (Plugin.OvWeekday.Value) { string id = WeekPlan.IdForNumber(env.CurrentDayNumber); if (id != null) val["Weekday"] = WeekPlan.DayName(id); }
                    if (Plugin.OvGameTime.Value)
                    {
                        // Tageszeit 0..1 = 0..24 Uhr (0,25 Sonnenaufgang, 0,5 Mittag); auf 10 Minuten gerundet wie eine Spieluhr
                        int min = Mathf.FloorToInt(Mathf.Repeat(env.TimeOfDay, 1f) * 1440f) / 10 * 10;
                        val["GameTime"] = (min / 60).ToString("00") + ":" + (min % 60).ToString("00");
                    }
                }
                catch { }
            }
            var parts = new List<string>();
            foreach (string k in OverlayOrder.Get()) if (val.TryGetValue(k, out string t)) parts.Add(t);
            if (parts.Count == 0) return "";
            if (Plugin.OvLayout.Value == "Column") return string.Join("\n", parts.ToArray());
            string sep;
            switch (Plugin.OvSeparator.Value)
            {
                case "Dash": sep = "  —  "; break;
                case "Bar": sep = "  |  "; break;
                case "Dot": sep = "  ·  "; break;
                default: sep = "   "; break;
            }
            return string.Join(sep, parts.ToArray());
        }

        // oben rechts steht im Spiel der Gebietsname - im Spiel darunter anfangen
        private float curScale = 1f;
        // Unter der Gebietsanzeige des Spiels (die mit Tag & Uhrzeit zweizeilig sein kann) anfangen
        private float TopFor(string corner)
        {
            if (corner != "TopRight" || !WeekPlan.InGame) return 12f;
            float top = 66f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f) / curScale;
            try
            {
                WorldZoneWidget wz = GUIElements.Instance != null ? GUIElements.Instance.WorldZoneWidget : null;
                if (wz != null && wz.gameObject.activeInHierarchy) top = Mathf.Max(top, GuiRect((RectTransform)wz.transform, curScale).yMax + 6f);
            }
            catch { }
            return top;
        }

        private Rect DrawOverlay(float scale)
        {
            if (string.IsNullOrEmpty(overlayText) || HudToggle.Hidden) return Rect.zero;
            var content = new GUIContent(overlayText);
            Vector2 size = overlayStyle.CalcSize(content);
            size.x += 4;
            float w = Screen.width / scale, h = Screen.height / scale, m = 12f;
            string c = Plugin.OvCorner.Value;
            float ins = GK2Tweaks.HudCenter.Inset(scale);
            float x = c.EndsWith("Right") ? w - size.x - m - ins : m + ins;
            float y = c.StartsWith("Top") ? TopFor(c) : h - size.y - m;
            Rect npc = NpcWidgetRect(scale);
            if (npc.width > 0 && npc.Overlaps(new Rect(x, y, size.x, size.y)))
                y = c.StartsWith("Top") ? npc.yMax + 6f : npc.y - 6f - size.y;
            var r = new Rect(x, y, size.x, size.y);
            GUI.Label(r, content, overlayStyle);
            return r;
        }

        // ---------- Pin-Liste ----------
        private GUIStyle pinTitleStyle, pinRowStyle, pinBoxStyle, pinXStyle, pinNoteStyle, pinInfoStyle;
        private string pinStyleKey;
        private Texture2D pinDarkTex;

        private void EnsurePinStyles()
        {
            string size = Plugin.PinsSize.Value;
            bool hc = Plugin.HighContrast.Value;
            string key = size + hc;
            if (pinTitleStyle != null && pinStyleKey == key) return;
            pinStyleKey = key;
            int f = size == "Small" ? 15 : size == "Large" ? 22 : size == "ExtraLarge" ? 26 : 18;
            pinBoxStyle = new GUIStyle(overlayStyle) { padding = new RectOffset(12, 12, 8, 8), fixedHeight = 0, fixedWidth = 0 };
            if (hc)
            {
                // Hoher Kontrast: fast schwarzer Hintergrund statt Schiefer
                if (pinDarkTex == null) { pinDarkTex = new Texture2D(1, 1); pinDarkTex.SetPixel(0, 0, new Color(0.03f, 0.03f, 0.04f, 0.92f)); pinDarkTex.Apply(); }
                pinBoxStyle.normal.background = pinDarkTex;
                pinBoxStyle.border = new RectOffset(0, 0, 0, 0);
            }
            pinTitleStyle = new GUIStyle(labelStyle) { fontSize = f + 1, fontStyle = FontStyle.Bold, fixedHeight = 0, wordWrap = false, alignment = TextAnchor.MiddleLeft, richText = true };
            pinTitleStyle.normal.textColor = hc ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.86f, 0.55f);
            pinRowStyle = new GUIStyle(labelStyle) { fontSize = f, fixedHeight = 0, wordWrap = false, alignment = TextAnchor.MiddleLeft, richText = true };
            if (hc) pinRowStyle.normal.textColor = Color.white;
            pinNoteStyle = new GUIStyle(pinRowStyle) { wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = Mathf.Max(12, f - 2) };
            pinInfoStyle = new GUIStyle(pinRowStyle) { fontSize = Mathf.Max(11, f - 1) };
            pinInfoStyle.normal.textColor = hc ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.72f, 0.7f, 0.62f);
            pinXStyle = new GUIStyle(pinRowStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            pinXStyle.normal.textColor = new Color(0.85f, 0.6f, 0.5f);
            pinXStyle.hover.textColor = Color.white;
        }

        private static string Count(Pins.Need n) => CountText(n.Have, n.Count);

        private static string CountText(int have, int count)
        {
            bool ok = have >= count;
            string col = Plugin.HighContrast.Value ? (ok ? "#7dff5c" : "#ff4d3d") : (ok ? "#9be27f" : "#ff7a6a");
            string t = have + " / " + count;
            return Plugin.HighContrast.Value ? "<b><color=" + col + ">" + t + "</color></b>" : "<color=" + col + ">" + t + "</color>";
        }

        private static readonly Vector3[] npcCorners = new Vector3[4];

        // Bildschirmbereich des NPC-Ansehen-Fensters (Portrait, Name, Ansehen) in GUI-Koordinaten, sonst leer
        private static Rect NpcWidgetRect(float scale)
        {
            try
            {
                UINpcWidget wdg = GUIElements.Instance != null ? GUIElements.Instance.NpcWidget : null;
                if (wdg == null || !wdg.gameObject.activeInHierarchy) return Rect.zero;
                var rt = wdg.transform as RectTransform;
                Canvas cv = wdg.GetComponentInParent<Canvas>();
                if (rt == null || cv == null) return Rect.zero;
                Camera cam = cv.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.rootCanvas.worldCamera;
                rt.GetWorldCorners(npcCorners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, npcCorners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, npcCorners[2]);
                float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
                if (x1 - x0 < 4f || y1 - y0 < 4f) return Rect.zero;
                return new Rect(x0 / scale, (Screen.height - y1) / scale, (x1 - x0) / scale, (y1 - y0) / scale);
            }
            catch { return Rect.zero; }
        }

        private void DrawPinsOld(float scale, Rect ov)
        {
            EnsurePinStyles();
            float line = pinRowStyle.fontSize + 10f, icon = line - 2f, w = 0f, h = 0f;
            float noteW = pinRowStyle.fontSize * 20f, indent = pinRowStyle.fontSize + 2f, btn = line - 4f;
            string ready = "  " + Labels.T("bereit", "ready"), done = "  " + Labels.T("erledigt", "done");
            // Breite und Hoehe vorab berechnen
            foreach (Pins.Pin p in Pins.List)
            {
                w = Mathf.Max(w, pinTitleStyle.CalcSize(new GUIContent(p.Title + ready)).x + icon + 38f + btn);
                h += line + 4f;
                if (p.Collapsed) { h += 6f; continue; }
                string head = PinHeader(p);
                if (head != null) { w = Mathf.Max(w, pinInfoStyle.CalcSize(new GUIContent(head)).x + 26f + (p.VarCount > 1 ? 2f * btn + 12f : 0f)); h += line; }
                if (p.Rows.Count > 0)
                    foreach (PinRow r in p.Rows)
                    {
                        string t = r.Info ? r.Name : r.Name + "   " + r.Have + " / " + r.Count;
                        w = Mathf.Max(w, (r.Info ? pinInfoStyle : pinRowStyle).CalcSize(new GUIContent(t)).x + icon + 26f + indent * r.Depth + btn);
                        h += line;
                    }
                else
                    foreach (Pins.Need n in p.Needs)
                    {
                        w = Mathf.Max(w, pinRowStyle.CalcSize(new GUIContent(n.Name + "   " + n.Have + " / " + n.Count)).x + icon + 22f);
                        h += line;
                    }
                if (p.FuelName != null) { w = Mathf.Max(w, pinRowStyle.CalcSize(new GUIContent(p.FuelName + "  ×" + p.FuelCount)).x + icon + 26f); h += line; }
                if (!string.IsNullOrEmpty(p.Note)) w = Mathf.Max(w, Mathf.Min(noteW, pinNoteStyle.CalcSize(new GUIContent(p.Note)).x + 30f));
                h += 6f;
            }
            w = Mathf.Max(w, 200f);
            foreach (Pins.Pin p in Pins.List)
                if (!p.Collapsed && !string.IsNullOrEmpty(p.Note)) h += Mathf.Min(pinNoteStyle.CalcHeight(new GUIContent(p.Note), w - 40f), pinNoteStyle.lineHeight * 4f + 4f);
            w += 24f; h += 12f;
            float sw = Screen.width / scale, sh = Screen.height / scale, m = 12f;
            string c = Plugin.PinsCorner.Value;
            float ins = GK2Tweaks.HudCenter.Inset(scale);
            float x = c.EndsWith("Right") ? sw - w - m - ins : m + ins;
            float y;
            if (c.StartsWith("Top")) y = ov.height > 0 ? ov.yMax + 6f : TopFor(c);
            else y = ov.height > 0 ? ov.y - 6f - h : sh - h - m;
            // Nicht ueber das Ansehen-Fenster der Haupt-NPCs legen (erscheint, wenn man vor ihnen steht)
            Rect npc = NpcWidgetRect(scale);
            if (npc.width > 0 && npc.Overlaps(new Rect(x, y, w, h)))
                y = c.StartsWith("Top") ? npc.yMax + 6f : npc.y - 6f - h;
            // sehr lange Liste: oben am Bildschirm halten
            if (y + h > sh - m && c.StartsWith("Top")) h = Mathf.Max(line * 3f, sh - m - y);
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, pinBoxStyle);
            float cy = y + 6f, cx = x + 12f, bottom = y + h - 4f;
            Pins.Pin remove = null;
            Action act = null;
            foreach (Pins.Pin p in Pins.List)
            {
                if (cy + line > bottom + 1f) break;
                Pins.Pin pp = p;
                if (GUI.Button(new Rect(cx - 4f, cy + 2f, btn, line - 4f), new GUIContent(p.Collapsed ? "+" : "-", p.Collapsed ? Labels.T("Aufklappen", "Expand") : Labels.T("Einklappen", "Collapse")), pinXStyle))
                    act = () => PinTree.ToggleCollapsed(pp);
                float tx = cx + btn;
                Texture2D ti = Pins.Icon(p.IconId);
                if (ti != null) { GUI.DrawTexture(new Rect(tx, cy, icon, icon), ti, ScaleMode.ScaleToFit); tx += icon + 6f; }
                string status = p.Ready ? "  <color=#9be27f>" + (p.QuestId != null ? done.Trim() : ready.Trim()) + "</color>" : "";
                GUI.Label(new Rect(tx, cy, w - (tx - x) - 36f, line), p.Title + status, pinTitleStyle);
                if (GUI.Button(new Rect(x + w - 30f, cy, 22f, line), new GUIContent("x", Labels.T("Loslösen", "Unpin")), pinXStyle)) remove = p;
                cy += line + 4f;
                if (p.Collapsed) { cy += 6f; continue; }
                if (!string.IsNullOrEmpty(p.Note))
                {
                    float nh = Mathf.Min(pinNoteStyle.CalcHeight(new GUIContent(p.Note), w - 40f), pinNoteStyle.lineHeight * 4f + 4f);
                    GUI.Label(new Rect(cx + 10f, cy, w - 40f, nh), p.Note, pinNoteStyle);
                    cy += nh;
                }
                string head = PinHeader(p);
                if (head != null && cy + line <= bottom + 1f)
                {
                    float hw = w - 36f;
                    if (p.VarCount > 1)
                    {
                        hw -= 2f * btn + 8f;
                        if (GUI.Button(new Rect(x + w - 30f - 2f * btn - 4f, cy + 2f, btn, line - 4f), new GUIContent("<", Labels.T("Andere Rezept-Variante", "Other recipe variant")), pinXStyle)) act = () => PinTree.Switch(pp, -1);
                        if (GUI.Button(new Rect(x + w - 30f - btn, cy + 2f, btn, line - 4f), new GUIContent(">", Labels.T("Andere Rezept-Variante", "Other recipe variant")), pinXStyle)) act = () => PinTree.Switch(pp, 1);
                    }
                    GUI.Label(new Rect(cx + 10f, cy, hw, line), head, pinInfoStyle);
                    cy += line;
                }
                if (p.Rows.Count > 0)
                {
                    foreach (PinRow r in p.Rows)
                    {
                        if (cy + line > bottom + 1f) break;
                        float rx = cx + 10f + indent * r.Depth;
                        if (r.Info)
                        {
                            GUI.Label(new Rect(rx, cy, w - (rx - x) - 12f, line), "· " + r.Name, pinInfoStyle);
                            cy += line;
                            continue;
                        }
                        if (r.Expandable)
                        {
                            string path = r.Path;
                            if (GUI.Button(new Rect(rx - 4f, cy + 2f, btn, line - 4f), new GUIContent(r.Expanded ? "-" : "+", r.Expanded ? Labels.T("Zutaten zuklappen", "Hide ingredients") : Labels.T("Zutaten dafür zeigen", "Show its ingredients")), pinXStyle))
                                act = () => PinTree.ToggleOpen(pp, path);
                        }
                        rx += btn;
                        Texture2D ni = Pins.Icon(r.IconId);
                        if (ni != null) GUI.DrawTexture(new Rect(rx, cy + 1f, icon - 2f, icon - 2f), ni, ScaleMode.ScaleToFit);
                        GUI.Label(new Rect(rx + icon + 4f, cy, w - (rx - x) - icon - 16f, line), r.Name + "   " + CountText(r.Have, r.Count), pinRowStyle);
                        cy += line;
                    }
                }
                else
                {
                    foreach (Pins.Need n in p.Needs)
                    {
                        if (cy + line > bottom + 1f) break;
                        Texture2D ni = Pins.Icon(n.IconId);
                        if (ni != null) GUI.DrawTexture(new Rect(cx + 10f, cy + 1f, icon - 2f, icon - 2f), ni, ScaleMode.ScaleToFit);
                        GUI.Label(new Rect(cx + icon + 14f, cy, w - icon - 30f, line), n.Name + "   " + Count(n), pinRowStyle);
                        cy += line;
                    }
                }
                if (p.FuelName != null && cy + line <= bottom + 1f)
                {
                    float fx = cx + 10f + btn;
                    Texture2D fi = Pins.Icon(p.FuelIcon);
                    if (fi != null) GUI.DrawTexture(new Rect(fx, cy + 1f, icon - 2f, icon - 2f), fi, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(fx + icon + 4f, cy, w - (fx - x) - icon - 16f, line), p.FuelName + "  ×" + p.FuelCount, pinInfoStyle);
                    cy += line;
                }
                cy += 6f;
            }
            if (remove != null) { Pins.Pin r = remove; Defer(() => Pins.Unpin(r)); }
            else if (act != null) Defer(act);
        }

        // "1/2 · Kreissaege ×4" unter dem Titel (nur Rezepte mit Varianten oder bekannter Werkbank)
        private static string PinHeader(Pins.Pin p)
        {
            if (p.VarCount == 0 || (p.VarCount < 2 && p.Station == null)) return null;
            string s = p.VarCount > 1 ? (p.VarIndex + 1) + "/" + p.VarCount : "";
            if (p.Station != null) s += (s.Length > 0 ? "  ·  " : "") + p.Station;
            if (p.OutCount > 1) s += "  ×" + p.OutCount;
            return s;
        }

        // Unsichtbare Flaeche ueber der Spiel-Oberflaeche, solange ein Mod-Fenster offen ist:
        // IMGUI-Klicks sollen nicht zusaetzlich Buttons des Spiels darunter ausloesen.
        private GameObject blocker;

        private void SetBlocker(bool on)
        {
            if (blocker == null)
            {
                if (!on) return;
                blocker = new GameObject("GK2VanillaPlus_ClickBlocker");
                DontDestroyOnLoad(blocker);
                var c = blocker.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 32000;
                blocker.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var img = new GameObject("Area", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
                img.transform.SetParent(blocker.transform, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                img.color = new Color(0, 0, 0, 0);
            }
            if (blocker.activeSelf != on) blocker.SetActive(on);
        }

        private void UpdateEnabled()
        {
            SetBlocker(menuOpen || newsOpen || celebOpen || Renaming
#if !NEXUS
                || rateOpen
#endif
            );
            if (weekOpen && !WeekPlan.InGame) weekOpen = false;
            bool want = menuOpen || weekOpen || newsOpen || celebOpen ||
#if !NEXUS
                rateOpen ||
#endif
                Plugin.ShowOverlay.Value || ManualSave.ShowMessage || GraphicsBench.Running || (Pins.List.Count > 0 && Plugin.PinsEnabled.Value) || InGameUiWant;
            if (enabled != want) enabled = want;
        }

        internal bool AnyWindowOpen => menuOpen || weekOpen || newsOpen || celebOpen
#if !NEXUS
            || rateOpen
#endif
            ;

        private void OnGUI()
        {
            if (HiResShot.Capturing) return;
            EnsureStyles();
            if (capturingKey != null && Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None)
            {
                if (Event.current.keyCode != KeyCode.Escape) capturingKey.Value = new KeyboardShortcut(Event.current.keyCode);
                else if (capturingKey != Plugin.MenuKey) capturingKey.Value = KeyboardShortcut.Empty;
                capturingKey = null;
                Event.current.Use();
            }

            PadBeginGUI();
            GUIStyle oldThumb = GUI.skin.verticalScrollbarThumb;
            if (skinned) GUI.skin.verticalScrollbarThumb = vthumbStyle;
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            if (Plugin.MenuScale.Value > 0) scale *= Plugin.MenuScale.Value / 100f;
            curScale = scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            Rect ov = Rect.zero;
            if (Plugin.ShowOverlay.Value) ov = DrawOverlay(scale);
            if (Pins.List.Count > 0 && Plugin.PinsEnabled.Value && WeekPlan.InGame && !HudToggle.Hidden && !BigGameWindowOpen())
                DrawPins(scale, Plugin.PinsCorner.Value == Plugin.OvCorner.Value ? ov : Rect.zero);
            if (GraphicsBench.Running)
            {
                var bm = new GUIContent(GraphicsBench.Status);
                Vector2 bsz = overlayStyle.CalcSize(bm);
                GUI.Label(new Rect((Screen.width / scale - bsz.x) / 2f, 24, bsz.x + 4, bsz.y), bm, overlayStyle);
            }
            else if (ManualSave.ShowMessage && !menuOpen)
            {
                var msg = new GUIContent(ManualSave.Message);
                Vector2 sz = overlayStyle.CalcSize(msg);
                GUI.Label(new Rect((Screen.width / scale - sz.x) / 2f, 24, sz.x + 4, sz.y), msg, overlayStyle);
            }
            InGameGUI(scale);
            DrawConfetti(scale);
            if (menuOpen) win = GUILayout.Window(WindowId, win, DrawWindow, skinned ? "" : "GK2 Vanilla+ · by McFly7", windowStyle);
            if (weekOpen)
            {
                if (weekWin.width <= 0) weekWin = new Rect(Screen.width / scale / 2f - 330, 80, 660, 10);
                weekWin = GUILayout.Window(WeekWindowId, weekWin, DrawWeekWindow, skinned ? "" : Labels.T("Wochenplan", "Week plan"), windowStyle);
            }
            if (newsOpen)
            {
                if (newsWin.width <= 0) newsWin = new Rect(Screen.width / scale / 2f - 380, 70, 760, 10);
                newsWin = GUILayout.Window(NewsWindowId, newsWin, DrawNewsWindow, skinned ? "" : Labels.T("Was ist neu?", "What's new?"), windowStyle);
            }
#if !NEXUS
            if (rateOpen)
            {
                // bewusst nicht zentriert: oben rechts, damit weder Logo/Menu-Buttons (Mitte) noch das große Mod-Fenster (oben links, 40/60) verdeckt werden
                if (rateWin.width <= 0) rateWin = new Rect(Screen.width / scale - 484, 24, 460, 10);
                rateWin = GUILayout.Window(RateWindowId, rateWin, DrawRateWindow, skinned ? "" : Labels.T("Gefällt dir der Mod?", "Enjoying the mod?"), windowStyle);
            }
#endif
            if (celebOpen) { DrawCelebration(scale); GUI.BringWindowToFront(CelebWindowId); }
            GUI.matrix = old;
            GUI.skin.verticalScrollbarThumb = oldThumb;
        }

        private GUIStyle warnStyle;
        private float footHeight;
        // Einmal pro Frame (in Tick, nicht live in OnGUI) gelesen: UpdateCheck.Available kann durch die
        // asynchrone GitHub-Abfrage genau zwischen dem Layout- und dem Repaint-Event umschlagen, was Unity
        // sonst mit "control count mismatch" quittiert (fehlendes/zusaetzliches GUILayout-Element).
        private bool updateAvailableSnap;
        private string safeNoticeSnap;

        private void DrawWindow(int id)
        {
            PadWindowBegin(WindowId);
            if (skinned) GUILayout.Label("GK2 Vanilla+  ·  by McFly7", titleStyle);
            if (updateAvailableSnap) DrawUpdate();
            GUILayout.Label(fpsText + "     " + SystemInfo.graphicsDeviceVersion, labelStyle);
            GUILayout.Label(Labels.T("Aktiv: ", "Active: ") + Plugin.DescribeFeatures(), smallStyle);
            string safe = safeNoticeSnap;
            if (safe != null)
            {
                if (warnStyle == null) { warnStyle = new GUIStyle(smallStyle) { wordWrap = true }; warnStyle.normal.textColor = new Color(1f, 0.62f, 0.3f); }
                GUILayout.Label(safe, warnStyle);
            }
            GUILayout.Space(4);

            DrawTabs();
            scroll = skinned ? GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, vbarStyle, GUIStyle.none, GUILayout.Height(520))
                             : GUILayout.BeginScrollView(scroll, GUILayout.Height(520));
            inScroll = true;
            if (menuTab == 0)
            {
                if (MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.MainMenu) DrawSaves();
                Header(Labels.T("Schnellstart", "Quick start"));
                DrawProfiles();
                DrawMaster();
                Header(Labels.T("Spiel", "Game"));
                DrawGameTier();
                DrawGfxBench();
            }
            else if (menuTab == 1)
            {
                Header(Labels.T("Bildrate", "Frame rate"));
                DrawEntry(Plugin.Pacing);
                DrawEntry(Plugin.TargetFps);
                DrawEntry(Plugin.NoTearing);

                Header(Labels.T("Grafik (überschreibt einzelne Teile der Grafikstufe)", "Graphics (overrides parts of the graphics tier)"));
                DrawEntry(Plugin.RenderMode);
                DrawEntry(Plugin.Shadows);
                DrawEntry(Plugin.Hbao);
                DrawEntry(Plugin.PointLights);
                DrawEntry(Plugin.BackLight);
                DrawEntry(Plugin.Water);
                DrawEntry(Plugin.Clouds);
                DrawEntry(Plugin.OledBlack);
                DrawEntry(Plugin.WideRain);
                DrawEntry(Plugin.RainAmount);

                Header(Labels.T("Leistung", "Performance"));
                DrawEntry(Plugin.PhysicsHz);
                DrawEntry(Plugin.GameLog);
                DrawEntry(Plugin.FasterTransitions);
                DrawEntry(Plugin.LessMemoryCleanup);
                if (Transitions.OtherMod) GUILayout.Label(Labels.T("„Instant Transitions“ ist installiert – dessen Einstellungen gelten.", "\"Instant Transitions\" is installed – its settings apply."), labelStyle);
                else if (Transitions.Count > 0) GUILayout.Label(string.Format(Labels.T("Türen / Reisen zuletzt: Ø {0} s ({1}×)", "Doors / travel recently: avg {0} s ({1}×)"), Transitions.Average.ToString("0.00"), Transitions.Count), labelStyle);
                if (WineFix.IsWine) DrawWineFix();
            }
            else if (menuTab == 2)
            {
                Header(Labels.T("Im Spiel", "In the game"));
                DrawEntry(Plugin.HudClock);
                if (HudClock.OtherMod) GUILayout.Label(Labels.T("„What time is it“ ist installiert – die HUD-Zeile bleibt aus.", "\"What time is it\" is installed – the HUD line stays off."), labelStyle);
                // immer zeichnen (nicht abhaengig vom Schalter darueber): sonst passt das IMGUI-Layout beim Umschalten nicht
                DrawEntry(Plugin.HudClockMode);
                DrawEntry(Plugin.HudClock12h);
                DrawEntry(Plugin.EscLeave);
                if (EscLeave.OtherMod) GUILayout.Label(Labels.T("„ESC to Leave“ ist installiert – dessen Funktion wird genutzt.", "\"ESC to Leave\" is installed – its function is used."), labelStyle);
                DrawEntry(Plugin.WeekPlanNotify);
                DrawEntry(Plugin.ZombieRename);
                DrawEntry(Plugin.TradeLikes);
                DrawEntry(Plugin.CraftMaxButton);
                DrawEntry(Plugin.PauseInBackground);
                DrawEntry(Plugin.AutoSaveMinutes);

                Header(Labels.T("Bauen", "Building"));
                DrawEntry(Plugin.InstantRemove);
                GUILayout.Label(Labels.T("Nicht mehr ganz Vanilla – oft gewünscht, deshalb als Option (standardmäßig aus):", "Not fully vanilla – often requested, so it's an option (off by default):"), smallStyle);
                DrawEntry(Plugin.FullRefund);
                DrawEntry(Plugin.MoveObjects);
                if (Respec.OtherMod) GUILayout.Label(Labels.T("„Talent & Tech Refund“ ist installiert – dessen Funktion wird genutzt.", "\"Talent & Tech Refund\" is installed – its function is used."), smallStyle);
                DrawEntry(Plugin.Respec);

                Header(Labels.T("Kamera", "Camera"));
                DrawEntry(Plugin.Zoom);
                DrawEntry(Plugin.InteriorZoom);
                DrawEntry(Plugin.ZoomPresets);
                DrawEntry(Plugin.ZoomPresetKey);
                DrawEntry(Plugin.MouseWheelZoom);
                DrawEntry(Plugin.SmoothZoom);

                DrawBackups();
            }
            else if (menuTab == 3)
            {
                DrawPinControls();
                Header(Labels.T("Anpinnen", "Pinning"));
                DrawEntry(Plugin.PinsEnabled);
                DrawEntry(Plugin.PinsChests);
                DrawEntry(Plugin.PinsNotify);
                DrawEntry(Plugin.PinsAutoUnpin);
                DrawEntry(Plugin.PinsVariants);
                DrawEntry(Plugin.PinsTree);
                DrawEntry(Plugin.PinsFuel);
                DrawEntry(Plugin.PinsPadButton);
                DrawEntry(Plugin.PinsCorner);
                DrawEntry(Plugin.PinsSize);
                if (Pins.List.Count > 0 && Btn(Labels.T("Alle Pins entfernen", "Remove all pins"), buttonStyle, GUILayout.Width(260))) Defer(Pins.ClearAll);
            }
            else
            {
                Header(Labels.T("Anzeige", "Interface"));
                DrawEntry(Plugin.Language);
                DrawEntry(Plugin.MenuScale);
                DrawEntry(Plugin.HighContrast);
                DrawEntry(Plugin.HudCenter);
                DrawEntry(Plugin.SkipIntro);
                DrawEntry(Plugin.MenuExtend);
                DrawEntry(Plugin.MenuModdedLabel);

                Header(Labels.T("Hauptmenü-Hintergrund", "Main menu background"));
                DrawEntry(Plugin.MenuBg);
                if (Plugin.MenuBg.Value != "Scene")
                {
                    DrawEntry(Plugin.MenuBgStyle);
                    DrawEntry(Plugin.MenuBgBlur);
                    DrawEntry(Plugin.MenuBgDim);
                    DrawEntry(Plugin.MenuBgFog);
                    if (Plugin.MenuBgFog.Value > 0) DrawEntry(Plugin.MenuBgFogTone);
                }
                if (WeekPlan.InGame)
                {
                    if (Btn(Labels.T("Aktuelle Ansicht als Menü-Hintergrund", "Use current view as menu background"), buttonStyle, GUILayout.Width(420))) Defer(MenuBackground.CaptureMine);
                    GUILayout.Label(Labels.T("Nimmt das Spielbild ohne HUD auf und stellt „Eigenes Bild“ ein. Zu sehen beim nächsten Besuch im Hauptmenü.", "Captures the game view without HUD and selects \"My picture\". Shown next time you are in the main menu."), smallStyle);
                }
                else if (Plugin.MenuBg.Value == "Mine" && !MenuBackground.HasMine)
                    GUILayout.Label(Labels.T("Noch kein eigenes Bild: im Spiel hier „Aktuelle Ansicht als Menü-Hintergrund“ wählen.", "No picture yet: in game, choose \"Use current view as menu background\" here."), smallStyle);

                DrawEntry(Plugin.GameMenuButton);
#if !NEXUS
                DrawEntry(Plugin.CheckUpdates);
#endif

                Header(Labels.T("Tasten", "Keys"));
                DrawEntry(Plugin.MenuKey);
                DrawEntry(Plugin.OverlayKey);
                DrawEntry(Plugin.SaveKey);
                DrawEntry(Plugin.WeekPlanKey);
                DrawEntry(Plugin.HudKey);

                Header(Labels.T("FPS-Anzeige", "FPS display"));
                foreach (ConfigEntryBase e in new ConfigEntryBase[] { Plugin.ShowOverlay, Plugin.OvCorner, Plugin.OvLayout, Plugin.OvSeparator })
                    DrawEntry(e);
                DrawOverlayItems();
                DrawEntry(Plugin.StatsLogSeconds);

                Header(Labels.T("Screenshots", "Screenshots"));
                DrawEntry(Plugin.ShotKey);
                DrawEntry(Plugin.ShotScale);
                DrawEntry(Plugin.ShotHideHud);
                if (Btn(Labels.T("Screenshot-Ordner öffnen", "Open screenshot folder"), buttonStyle, GUILayout.Width(260)))
                {
                    System.IO.Directory.CreateDirectory(HiResShot.Folder);
                    Application.OpenURL("file:///" + HiResShot.Folder.Replace('\\', '/'));
                }

                foreach (PluginInfo info in Chainloader.PluginInfos.Values)
                {
                    if (info.Instance == null || info.Instance == Plugin.Instance) continue;
                    ConfigFile cfg = info.Instance.Config;
                    if (cfg == null || cfg.Count == 0) continue;
                    Header(info.Metadata.Name + Labels.T(" (wirkt nach Neustart)", " (applies after restart)"));
                    foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in cfg) DrawEntry(kv.Value);
                }
            }
            GUILayout.EndScrollView();
            inScroll = false;
            PadScrollInto(ref scroll, 520f);
            if (Take(ref padLB)) Defer(() => SetTab(menuTab - 1));
            if (Take(ref padRB)) Defer(() => SetTab(menuTab + 1));
            if (scrollToBench && Event.current.type == EventType.Repaint) { scroll.y = Mathf.Max(0, benchY - 10); scrollToBench = false; }
            if (pendingScrollHeader != null && Event.current.type == EventType.Repaint && headerY.TryGetValue(pendingScrollHeader, out float hy)) { scroll.y = Mathf.Max(0, hy - 10); pendingScrollHeader = null; }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (Btn(Labels.T("Jetzt speichern", "Save now"), buttonStyle)) ManualSave.Save(true);
            if (Btn(Labels.T("Was ist neu?", "What's new?"), buttonStyle)) Defer(() => ShowNews(false));
            // freiwillige Unterstuetzung: oeffnet nur die Ko-fi-Seite im Browser, keine Funktion haengt daran
            if (Btn(new GUIContent(Labels.T("Unterstützen", "Support"), Labels.T("Vanilla+ bleibt kostenlos. Wenn dir der Mod gefällt, freue ich mich über einen Kaffee auf Ko-fi (öffnet den Browser).", "Vanilla+ stays free. If you enjoy the mod, a coffee on Ko-fi is very welcome (opens your browser).")), buttonStyle)) Application.OpenURL(Plugin.KofiUrl);
            if (Btn(Labels.T("Grafik zurücksetzen", "Reset graphics"), buttonStyle)) ResetTweaks();
            if (Btn(Labels.T("Schließen (", "Close (") + Plugin.MenuKey.Value + ")", buttonStyle)) Defer(() => SetMenu(false));
            GUILayout.EndHorizontal();
            string foot = ManualSave.ShowMessage ? ManualSave.Message : (PadFooter() ?? GUI.tooltip);
            if (tipStyle == null || tipStyle.fontSize != (Plugin.HighContrast.Value ? 17 : 15))
            {
                tipStyle = new GUIStyle(smallStyle) { fontSize = Plugin.HighContrast.Value ? 17 : 15, wordWrap = true };
                tipStyle.normal.textColor = Plugin.HighContrast.Value ? Color.white : new Color(0.86f, 0.82f, 0.74f);
            }
            // Hoehe passend zum Text (mind. 3 Zeilen), damit lange Erklaerungen nicht abgeschnitten werden
            var footContent = new GUIContent(string.IsNullOrEmpty(foot) ? " " : foot);
            float footH = Mathf.Max(tipStyle.lineHeight * 3f + 6f, tipStyle.CalcHeight(footContent, win.width - 40f) + 6f);
            if (Event.current.type == EventType.Layout) footHeight = Mathf.Max(footHeight, footH);
            GUILayout.Label(footContent, tipStyle, GUILayout.Height(footHeight));
            PadWindowEnd();
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, win.width, win.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private GUIStyle wineInfoStyle;

        // Nur unter Wine/CrossOver sichtbar: Controller-Fix (Registry-Schalter in der Wine-Umgebung)
        private void DrawWineFix()
        {
            Header(Labels.T("Mac / Linux (CrossOver, Wine)", "Mac / Linux (CrossOver, Wine)"));
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Controller-Fix", "Controller fix"), Labels.T(
                "Gegen Ruckler mit PlayStation-Controllern (DualSense/DualShock) unter CrossOver/Wine: Wine liest den Controller dann über einen anderen, sparsameren Weg. Bei Problemen mit dem Controller wieder ausschalten.",
                "Fixes stutter with PlayStation controllers (DualSense/DualShock) under CrossOver/Wine by reading the controller through a cheaper path. Turn it off again if the controller misbehaves.")),
                labelStyle, GUILayout.Width(268));
            bool on = WineFix.Enabled;
            if (Btn(on ? Labels.T("An", "On") : Labels.T("Aus", "Off"), buttonStyle, GUILayout.Width(310))) WineFix.Enabled = !on;
            GUILayout.EndHorizontal();
            if (wineInfoStyle == null) wineInfoStyle = new GUIStyle(smallStyle) { wordWrap = true };
            GUILayout.Label(Labels.T(
                "Wofür? Mit PlayStation-Controllern (DualSense, DualShock 4) ruckelt das Spiel unter CrossOver/Wine oft spürbar, obwohl die FPS hoch sind. Grund: Wine liest diese Controller standardmäßig über einen aufwendigen Rohdaten-Weg (hidraw), der pro Bild mehrere Millisekunden kostet.\n" +
                "Was passiert? Der Schalter setzt in der Wine-Registry den Wert „DisableHidraw“. Wine liest den Controller dann über den normalen, sparsamen Weg – Tasten und Sticks funktionieren wie gewohnt; nur Sonderfunktionen wie Touchpad-Klick, Lichtleiste oder adaptive Trigger können wegfallen.\n" +
                "Wann einschalten? Wenn du mit PlayStation-Controller spielst und es ruckelt. Mit Xbox- oder anderen Controllern ist er nicht nötig. Er gilt für die ganze CrossOver-Flasche bzw. das Proton-Prefix und lässt sich jederzeit wieder ausschalten.",
                "What for? With PlayStation controllers (DualSense, DualShock 4) the game often stutters noticeably under CrossOver/Wine even though the FPS are high. Reason: by default Wine reads these controllers through an expensive raw-data path (hidraw) that costs several milliseconds per frame.\n" +
                "What does it do? The switch sets the value \"DisableHidraw\" in the Wine registry. Wine then reads the controller through the normal, cheaper path – buttons and sticks work as usual; only extras like touchpad click, light bar or adaptive triggers may stop working.\n" +
                "When to turn it on? If you play with a PlayStation controller and the game stutters. Not needed for Xbox or other controllers. It applies to the whole CrossOver bottle / Proton prefix and can be turned off again at any time."), wineInfoStyle);
            if (WineFix.Pending)
                GUILayout.Label(Labels.T("Wirkt nach Neustart: Spiel UND Steam beenden (bzw. die CrossOver-Flasche neu starten).",
                    "Applies after a restart: quit the game AND Steam (or restart the CrossOver bottle)."), smallStyle);
        }

        // ---------- Grafik-Benchmark ----------
        private bool scrollToBench;
        private float benchY;

        internal void ShowBenchResults() { menuTab = 0; scrollToBench = true; }

        private void DrawGfxBench()
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Grafik-Benchmark", "Graphics benchmark"), Labels.T(
                "Geht alle Grafikstufen nacheinander durch (je ca. 13 s, insgesamt gut 1 Minute) und misst die FPS ohne Limit. Am Ende gibt es einen Score und eine Empfehlung. Nichts wird gespeichert, deine Einstellungen bleiben wie sie sind. Am besten an einer typischen Stelle stehen bleiben. Esc bricht ab.",
                "Runs through all graphics tiers (about 13 s each, a bit over a minute in total) and measures FPS without a limit. You get a score and a recommendation at the end. Nothing is saved, your settings stay as they are. Best to stand still at a typical spot. Esc cancels.")),
                labelStyle, GUILayout.Width(268));
            GUI.enabled = GraphicsBench.CanRun;
            if (Btn(GraphicsBench.CanRun ? Labels.T("Benchmark starten", "Start benchmark") : Labels.T("nur im laufenden Spiel", "only while playing"), buttonStyle, GUILayout.Width(310)))
                Defer(GraphicsBench.Start);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (Event.current.type == EventType.Repaint) benchY = GUILayoutUtility.GetLastRect().y;
            if (GraphicsBench.Results.Count == 0) return;
            foreach (GraphicsBench.Step st in GraphicsBench.Results)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(st.Name, labelStyle, GUILayout.Width(360));
                GUILayout.Label(st.Fps.ToString("0") + " FPS", labelStyle, GUILayout.Width(110));
                GUILayout.Label("1%: " + st.Low.ToString("0"), labelStyle, GUILayout.Width(110));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Score: " + GraphicsBench.Score, headerStyle);
            GUILayout.Label(GraphicsBench.Recommendation, new GUIStyle(labelStyle) { wordWrap = true, fixedHeight = 0 });
            GUILayout.Label(Labels.T("Score = mittlere FPS über die vier Grafikstufen × 10, höher ist besser – gut zum Vergleichen von PCs und Einstellungen. Gespeichert in BepInEx/GK2VanillaPlus/benchmark.txt",
                "Score = average FPS across the four graphics tiers × 10, higher is better – handy for comparing PCs and settings. Saved to BepInEx/GK2VanillaPlus/benchmark.txt"), smallStyle);
        }

        // ---------- Was ist neu? ----------
        private void DrawNewsWindow(int id)
        {
            PadWindowBegin(NewsWindowId);
            if (newsStyle == null)
            {
                newsStyle = new GUIStyle(labelStyle) { fixedHeight = 0, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, fontSize = 15 };
            }
            if (skinned) GUILayout.Label(Labels.T("Was ist neu?", "What's new?") + "  ·  GK2 Vanilla+ " + Plugin.PluginVersion, titleStyle);
            if (newsSinceUpdate) GUILayout.Label(Labels.T("GK2 Vanilla+ wurde aktualisiert. Das hat sich geändert:", "GK2 Vanilla+ was updated. Here is what changed:"), smallStyle);
            newsScroll = skinned ? GUILayout.BeginScrollView(newsScroll, false, true, GUIStyle.none, vbarStyle, GUIStyle.none, GUILayout.Height(460))
                                 : GUILayout.BeginScrollView(newsScroll, GUILayout.Height(460));
            var list = newsSinceUpdate ? Changelog.Since(Plugin.LastSeenVersion.Value) : Changelog.Entries;
            foreach (Changelog.Entry e in list)
            {
                GUILayout.Label(Labels.T("Version ", "Version ") + e.Version + "   ·   " + e.Date, headerStyle);
                GUILayout.Label(e.Text, newsStyle);
                GUILayout.Space(6);
            }
            if (list.Count == 0) GUILayout.Label("–", newsStyle);
            GUILayout.EndScrollView();
            if (padMode && curWin == padWin && Event.current.type == EventType.Layout)
            {
                // Controller: hoch/runter scrollt den Text, die Buttons erreicht man mit links/rechts
                if (Take(ref padUp)) newsScroll.y = Mathf.Max(0, newsScroll.y - 120f);
                if (Take(ref padDown)) newsScroll.y += 120f;
            }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (newsSinceUpdate && Btn(Labels.T("Alle Versionen", "All versions"), buttonStyle)) Defer(() => { newsSinceUpdate = false; newsScroll = Vector2.zero; });
            if (Btn(Labels.T("Schließen", "Close"), buttonStyle)) Defer(CloseNews);
            GUILayout.EndHorizontal();
            PadWindowEnd();
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, newsWin.width, newsWin.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

#if !NEXUS
        // ---------- Bewertungshinweis ----------
        private void DrawRateWindow(int id)
        {
            PadWindowBegin(RateWindowId);
            if (newsStyle == null)
            {
                newsStyle = new GUIStyle(labelStyle) { fixedHeight = 0, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, fontSize = 15 };
            }
            if (skinned) GUILayout.Label(Labels.T("Gefällt dir der Mod?", "Enjoying the mod?"), titleStyle);
            GUILayout.Label(Labels.T(
                "Wenn dir GK2 Vanilla+ gefällt, würde mich eine kurze Bewertung (Daumen hoch) im Steam Workshop riesig freuen – das hilft anderen, den Mod zu finden. Danke dir! ❤",
                "If you're enjoying GK2 Vanilla+, a quick thumbs-up rating on the Steam Workshop would mean a lot – it helps other people find it. Thank you! ❤"), newsStyle);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (Btn(Labels.T("👍 Jetzt bewerten", "👍 Rate it now"), buttonStyle))
                Defer(() => { Application.OpenURL(Plugin.WorkshopUrl); CloseRate(0); });
            if (Btn(Labels.T("Später erinnern", "Remind me later"), buttonStyle)) Defer(() => CloseRate(6));
            if (Btn(Labels.T("Nicht mehr fragen", "Don't ask again"), buttonStyle)) Defer(() => CloseRate(0));
            GUILayout.EndHorizontal();
            PadWindowEnd();
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, rateWin.width, rateWin.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }
#endif

        // ---------- Wochenplan (F6) ----------
        private void DrawWeekWindow(int id)
        {
            PadWindowBegin(WeekWindowId);
            if (skinned) GUILayout.Label(Labels.T("Wochenplan", "Week plan") + "  ·  " + Labels.T("was geht wann?", "what's on when?"), titleStyle);
            int today = WeekPlan.TodayNumber;
            for (int k = 0; k < 6; k++)
            {
                int num = (today - 1 + k) % 6 + 1;
                string dayId = WeekPlan.IdForNumber(num);
                if (dayId == null) continue;
                var items = WeekPlan.ItemsFor(dayId);
                GUILayout.BeginHorizontal();
                Texture2D icon = WeekPlan.Icon(dayId);
                Rect ir = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44), GUILayout.Height(44));
                if (icon != null) { icon.filterMode = FilterMode.Point; GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit); }
                string head = WeekPlan.DayName(dayId) + (k == 0 ? Labels.T("  · heute", "  · today") : k == 1 ? Labels.T("  · morgen", "  · tomorrow") : "");
                GUILayout.BeginVertical();
                GUILayout.Label(head, k == 0 ? headerStyle : labelStyle);
                GUILayout.Label(items.Count == 0 ? Labels.T("– nichts Besonderes (oder noch nicht freigeschaltet)", "– nothing special (or not unlocked yet)") : "• " + string.Join("\n• ", items.ToArray()), smallStyle);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            GUILayout.Space(4);
            if (Btn(Labels.T("Schließen (", "Close (") + Plugin.WeekPlanKey.Value + ")", buttonStyle)) Defer(() => { weekOpen = false; UpdateEnabled(); });
            PadWindowEnd();
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, weekWin.width, weekWin.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private GUIStyle tipStyle;

        // ---------- Spielstaende (nur Hauptmenue): Uebersicht, direkt spielen, Backups je Slot ----------
        private List<SaveSlotData> slots = new List<SaveSlotData>();
        private float slotsAt;

        private void DrawSaves()
        {
            Header(Labels.T("Spielstände", "Saves"));
            if (Event.current.type == EventType.Layout && Time.realtimeSinceStartup > slotsAt)
            {
                try { slots = new List<SaveSlotData>(SaveSystem.SaveSlotDataList); slots.Sort((a, b) => b.GetSaveDateTime().CompareTo(a.GetSaveDateTime())); }
                catch (Exception e) { Plugin.Log.LogWarning("Saves: " + e.Message); slots = new List<SaveSlotData>(); }
                if (Event.current.type == EventType.Layout && Time.realtimeSinceStartup > backupListAt) { backupList = Backups.List(); backupListAt = Time.realtimeSinceStartup + 3f; }
                slotsAt = Time.realtimeSinceStartup + 3f;
            }
            if (slots.Count == 0) { GUILayout.Label(Labels.T("Keine Spielstände gefunden.", "No saves found."), smallStyle); return; }
            string fmt = Labels.German ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm";
            foreach (SaveSlotData sd in slots)
            {
                if (sd == null || sd.slotName == null || sd.slotName.Contains("_backup_")) continue;
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(460));
                DateTime dt = sd.GetSaveDateTime();
                GUILayout.Label("<b>" + sd.slotName + "</b>   " + Labels.T("Tag ", "Day ") + sd.day + (sd.isAutoSave ? Labels.T("   (Autosave)", "   (autosave)") : ""), richLabel);
                GUILayout.Label((dt == default(DateTime) ? "" : dt.ToString(fmt) + "   ·   ") + Labels.T("Friedhof ", "Graveyard ") + sd.graveyardQuality + "   ·   " + Labels.T("Kirche ", "Church ") + sd.churchQuality, smallStyle);
                GUILayout.EndVertical();
                if (Btn(Labels.T("Spielen", "Play"), buttonStyle, GUILayout.Width(110))) Defer(() => LoadSlot(sd));
                GUILayout.EndHorizontal();
                // Kopien, die das Spiel selbst anlegt (Steam_1_backup_1 ...): direkt spielbar
                foreach (SaveSlotData gb in slots)
                {
                    if (gb == null || gb.slotName == null || !gb.slotName.StartsWith(sd.slotName + "_backup_")) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    DateTime gdt = gb.GetSaveDateTime();
                    GUILayout.Label(Labels.T("Kopie des Spiels ", "Game's own copy ") + gb.slotName.Substring(sd.slotName.Length + 8) + "   " + Labels.T("Tag ", "Day ") + gb.day + (gdt == default(DateTime) ? "" : "   " + gdt.ToString(fmt)), smallStyle, GUILayout.Width(436));
                    if (Btn(Labels.T("Spielen", "Play"), buttonStyle, GUILayout.Width(110))) Defer(() => LoadSlot(gb));
                    GUILayout.EndHorizontal();
                }
                foreach (Backups.Item b in backupList)
                {
                    if (b.Slot != sd.slotName) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(24);
                    string when = b.Time == default(DateTime) ? System.IO.Path.GetFileName(b.Dir) : b.Time.ToString(fmt);
                    GUILayout.Label(Labels.T("Backup ", "Backup ") + when + (b.Dir.EndsWith("_restore") ? Labels.T("  (vor Wiederherstellung)", "  (before restore)") : "") + (b.Kept ? Labels.T("  · behalten", "  · kept") : ""), smallStyle, GUILayout.Width(436));
                    KeepButton(b);
                    bool confirm = confirmRestore == b;
                    if (Btn(confirm ? Labels.T("Sicher?", "Sure?") : Labels.T("Laden", "Restore"), buttonStyle, GUILayout.Width(110)))
                    {
                        if (!confirm) confirmRestore = b;
                        else { Backups.Item rb = b; Defer(() => { Backups.Restore(rb, out backupMsg); confirmRestore = null; backupListAt = 0; slotsAt = 0; }); }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(4);
            }
            if (!string.IsNullOrEmpty(backupMsg)) GUILayout.Label(backupMsg, smallStyle);
        }

        // Backup behalten / freigeben (behaltene werden nie automatisch geloescht und zaehlen nicht zur Anzahl)
        private void KeepButton(Backups.Item b)
        {
            if (Btn(b.Kept ? Labels.T("Freigeben", "Unkeep") : Labels.T("Behalten", "Keep"), buttonStyle, GUILayout.Width(120)))
            {
                Backups.Item kb = b;
                Defer(() => { Backups.SetKept(kb, !kb.Kept); backupListAt = 0; });
            }
        }

        private GUIStyle richLabelStyle;
        private GUIStyle richLabel => richLabelStyle ?? (richLabelStyle = new GUIStyle(labelStyle) { richText = true, fixedHeight = 0 });

        // Wie "Fortsetzen" im Hauptmenue, nur fuer einen bestimmten Spielstand
        private void LoadSlot(SaveSlotData sd)
        {
            try
            {
                OnMenuClosed = null;
                SetMenu(false);
                UIMainMenuWindow mm = LazyBearTechnology.LazyUI.GetWindow<UIMainMenuWindow>();
                UILoadingOverlay overlay = LazyBearTechnology.LazyUI.Get<UILoadingOverlay>();
                overlay.Draw(new LoadingWindowData(MainGame.EntrySceneToLoad, () =>
                {
                    SaveSystem.Load(sd, (GameSave save) =>
                    {
                        if (save != null) { MainGame.Instance.ContinueGame(sd, save); mm?.Close(); }
                        else { overlay.Hide(); mm?.Open(null); }
                    });
                }));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Load slot: " + e.Message); }
        }

        // ---------- Spielstand-Backups ----------
        private void DrawBackups()
        {
            Header(Labels.T("Spielstand-Backups", "Save backups"));
            DrawEntry(Plugin.BackupCount);
            DrawEntry(Plugin.BackupMinutes);
            if (Event.current.type == EventType.Layout && Time.realtimeSinceStartup > backupListAt) { backupList = Backups.List(); backupListAt = Time.realtimeSinceStartup + 3f; }
            bool inMenu = MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.MainMenu;
            if (backupList.Count == 0) GUILayout.Label(Labels.T("Noch keine Backups vorhanden.", "No backups yet."), smallStyle);
            if (inMenu) GUILayout.Label(Labels.T("Backups laden: oben unter \"Spielstände\".", "Restore backups: see \"Saves\" at the top."), smallStyle);
            else foreach (Backups.Item b in backupList)
            {
                GUILayout.BeginHorizontal();
                string when = b.Time == default(System.DateTime) ? System.IO.Path.GetFileName(b.Dir) : b.Time.ToString(Labels.German ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
                GUILayout.Label(when + "   " + b.Slot + "   " + (b.Bytes / 1048576f).ToString("0.0") + " MB" + (b.Dir.EndsWith("_restore") ? Labels.T("  (vor Wiederherstellung)", "  (before restore)") : "") + (b.Kept ? Labels.T("  · behalten", "  · kept") : ""), labelStyle, GUILayout.Width(460));
                KeepButton(b);
                if (inMenu)
                {
                    bool confirm = confirmRestore == b;
                    if (Btn(confirm ? Labels.T("Sicher?", "Sure?") : Labels.T("Laden", "Restore"), buttonStyle, GUILayout.Width(110)))
                    {
                        if (!confirm) confirmRestore = b;
                        else { Backups.Item rb = b; Defer(() => { Backups.Restore(rb, out backupMsg); confirmRestore = null; backupListAt = 0; }); }
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(!string.IsNullOrEmpty(backupMsg) ? backupMsg : (inMenu ? Labels.T("\"Laden\" ersetzt den Spielstand durch das Backup (der aktuelle Stand wird vorher gesichert). \"Behalten\" schützt ein Backup vor dem automatischen Löschen.", "\"Restore\" replaces the save with the backup (the current save is backed up first). \"Keep\" protects a backup from automatic deletion.")
                : Labels.T("Wiederherstellen ist im Hauptmenü möglich.", "Restoring is available in the main menu.")), smallStyle);
            if (Btn(Labels.T("Backup-Ordner öffnen", "Open backup folder"), buttonStyle, GUILayout.Width(260)))
            {
                System.IO.Directory.CreateDirectory(Backups.Root);
                Application.OpenURL("file:///" + Backups.Root.Replace('\\', '/'));
            }
        }

        private void DrawUpdate()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Labels.T("Update verfügbar: ", "Update available: ") + Plugin.PluginVersion + " → " + UpdateCheck.Latest, headerStyle, GUILayout.Width(300));
            if (UpdateCheck.CanAutoUpdate)
            {
                if (Btn(new GUIContent(Labels.T("Speichern & aktualisieren", "Save & update"),
                        Labels.T("Speichert (falls im Spiel), beendet das Spiel und installiert die neue Version. Danach kann das Spiel direkt neu gestartet werden.",
                                 "Saves (if in game), quits the game and installs the new version. The game can be restarted right after.")), buttonStyle))
                    UpdateCheck.SaveAndUpdate(true);
            }
            if (Btn(Labels.T("Download-Seite", "Download page"), buttonStyle, GUILayout.Width(150))) Application.OpenURL(UpdateCheck.ReleasePage);
            GUILayout.EndHorizontal();
        }

        private readonly Dictionary<string, float> headerY = new Dictionary<string, float>();
        private string pendingScrollHeader;

        internal void ScrollToHeader(string text) { if (text == Labels.T("Spielstand-Backups", "Save backups")) menuTab = 2; pendingScrollHeader = text; }

        private void Header(string text)
        {
            GUILayout.Space(8);
            GUILayout.Label(text, headerStyle);
            if (Event.current.type == EventType.Repaint) headerY[text] = GUILayoutUtility.GetLastRect().y;
        }

        // Ein-Klick-Profile (Grafik + Bildrate)
        private void DrawProfiles()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Profil", "Profile"), Labels.T("Setzt mehrere Einstellungen auf einmal. Danach bleibt alles einzeln änderbar.", "Sets several options at once. Everything stays adjustable afterwards.")), labelStyle, GUILayout.Width(268));
            GUILayout.BeginVertical();
            for (int r = 0; r < 2; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 2; c++)
                {
                    string id = Profiles.Ids[r * 2 + c];
                    if (Btn(new GUIContent(Profiles.Name(id), Profiles.Tip(id)), buttonStyle, GUILayout.Width(153))) { string pid = id; Defer(() => Profiles.Apply(pid)); }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawGameTier()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Grafikstufe", "Graphics tier"), Labels.T("Die Stufe aus dem Spielmenü. Die Werte unter 'Grafik' überschreiben einzelne Teile davon.", "The tier from the game menu. The values under 'Graphics' override parts of it.")), labelStyle, GUILayout.Width(268));
            var tiers = (GraphicsTier[])Enum.GetValues(typeof(GraphicsTier));
            int i = Math.Max(0, Array.IndexOf(tiers, gs.graphicsTier));
            bool pf = PadFocused();
            if (GUILayout.Button("<", arrowStyle, GUILayout.Width(36))) SetTier(tiers[(i - 1 + tiers.Length) % tiers.Length]);
            GUILayout.Label(Labels.Tier(gs.graphicsTier), valueStyle, GUILayout.Width(230));
            if (GUILayout.Button(">", arrowStyle, GUILayout.Width(36))) SetTier(tiers[(i + 1) % tiers.Length]);
            GUILayout.EndHorizontal();
            PadMark(pf);
            if (pf)
            {
                padTip = Labels.T("Die Stufe aus dem Spielmenü.", "The tier from the game menu.");
                if (Take(ref padLeft)) SetTier(tiers[(i - 1 + tiers.Length) % tiers.Length]);
                else if (Take(ref padRight) || Take(ref padA)) SetTier(tiers[(i + 1) % tiers.Length]);
            }
        }

        private static void SetTier(GraphicsTier tier)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            gs.graphicsTier = tier;
            try { gs.ApplyGraphicsTier(applySave: true); } catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
        }

        // Werte der FPS-Anzeige: an/aus und Reihenfolge (Pfeile)
        private void DrawOverlayItems()
        {
            List<string> order = OverlayOrder.Get();
            for (int i = 0; i < order.Count; i++)
            {
                ConfigEntry<bool> e = OverlayOrder.Entry(order[i]);
                if (e == null) continue;
                bool pf = PadFocused();
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(Labels.Name(e), Labels.Tip(e)), labelStyle, GUILayout.Width(268));
                if (GUILayout.Button(e.Value ? Labels.T("An", "On") : Labels.T("Aus", "Off"), buttonStyle, GUILayout.Width(226))) e.Value = !e.Value;
                GUI.enabled = i > 0;
                if (GUILayout.Button(new GUIContent("^", Labels.T("Nach vorne", "Move up")), arrowStyle, GUILayout.Width(40))) OverlayOrder.Move(i, -1);
                GUI.enabled = i < order.Count - 1;
                if (GUILayout.Button(new GUIContent("v", Labels.T("Nach hinten", "Move down")), arrowStyle, GUILayout.Width(40))) OverlayOrder.Move(i, 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                PadMark(pf);
                if (pf)
                {
                    padTip = Labels.Tip(e) + "  " + Labels.T("(LB/RB: verschieben)", "(LB/RB: move)");
                    if (Take(ref padA) || Take(ref padLeft) || Take(ref padRight)) e.Value = !e.Value;
                    if (Take(ref padLB) && i > 0) { OverlayOrder.Move(i, -1); padFocus--; }
                    if (Take(ref padRB) && i < order.Count - 1) { OverlayOrder.Move(i, 1); padFocus++; }
                }
            }
        }

        private void DrawEntry(ConfigEntryBase e)
        {
            bool pf = PadFocused();
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.Name(e), Labels.Tip(e)), labelStyle, GUILayout.Width(268));
            AcceptableValueBase acc = e.Description?.AcceptableValues;
            if (e == Plugin.PinsPadButton) PadBindField(Plugin.PinsPadButton, pf);
            else if (e.SettingType == typeof(bool))
            {
                bool v = (bool)e.BoxedValue;
                if (GUILayout.Button(v ? Labels.T("An", "On") : Labels.T("Aus", "Off"), buttonStyle, GUILayout.Width(310))) e.BoxedValue = !v;
            }
            else if (acc is AcceptableValueList<string> ls) Cycle(e, ls.AcceptableValues.Cast<object>().ToArray());
            else if (acc is AcceptableValueList<int> li) Cycle(e, li.AcceptableValues.Cast<object>().ToArray());
            else if (e.SettingType == typeof(KeyboardShortcut)) KeyField((ConfigEntry<KeyboardShortcut>)e);
            else TextField(e);
            GUILayout.EndHorizontal();
            PadMark(pf);
            object[] vals = acc is AcceptableValueList<string> ls2 ? ls2.AcceptableValues.Cast<object>().ToArray()
                : acc is AcceptableValueList<int> li2 ? li2.AcceptableValues.Cast<object>().ToArray() : null;
            PadEntry(pf, e, vals);
        }

        private void Cycle(ConfigEntryBase e, object[] values)
        {
            int i = Math.Max(0, Array.IndexOf(values, e.BoxedValue));
            if (GUILayout.Button("<", arrowStyle, GUILayout.Width(36))) e.BoxedValue = values[(i - 1 + values.Length) % values.Length];
            GUILayout.Label(Labels.Value(e, values[i]), valueStyle, GUILayout.Width(230));
            if (GUILayout.Button(">", arrowStyle, GUILayout.Width(36))) e.BoxedValue = values[(i + 1) % values.Length];
        }

        // Controller-Taste frei belegen: Feld anklicken (oder A), dann beliebige Controller-Taste druecken; Esc = aus
        private void PadBindField(ConfigEntry<string> e, bool focused)
        {
            bool cap = PadBind.Capturing == e;
            string text = cap ? Labels.T("Controller-Taste drücken … (Esc = aus)", "Press a controller button … (Esc = off)") : PadBind.Display(e.Value);
            bool click = GUILayout.Button(text, buttonStyle, GUILayout.Width(310));
            if (!cap && focused && Take(ref padA)) click = true;
            if (click && !cap) Defer(() => PadBind.Start(e));
        }

        private void KeyField(ConfigEntry<KeyboardShortcut> e)
        {
            string text = capturingKey == e ? Labels.T("Taste drücken … (Esc = keine)", "Press a key … (Esc = none)") : (e.Value.MainKey == KeyCode.None ? Labels.T("– keine –", "– none –") : e.Value.ToString());
            if (GUILayout.Button(text, buttonStyle, GUILayout.Width(310))) capturingKey = e;
        }

        private void TextField(ConfigEntryBase e)
        {
            if (!textBuffers.TryGetValue(e, out string buf)) buf = e.GetSerializedValue();
            buf = GUILayout.TextField(buf ?? "", GUILayout.Width(240));
            textBuffers[e] = buf;
            if (GUILayout.Button("OK", buttonStyle, GUILayout.Width(64)))
            {
                try { e.SetSerializedValue(buf); textBuffers.Remove(e); }
                catch (Exception ex) { Plugin.Log.LogWarning("Ungueltiger Wert: " + ex.Message); }
            }
        }

        private static void ResetTweaks()
        {
            foreach (ConfigEntryBase e in new ConfigEntryBase[] { Plugin.Pacing, Plugin.TargetFps, Plugin.RenderMode, Plugin.Shadows, Plugin.Hbao,
                         Plugin.PointLights, Plugin.BackLight, Plugin.Water, Plugin.Clouds, Plugin.PhysicsHz, Plugin.GameLog })
                e.BoxedValue = e.DefaultValue;
        }

        private void EnsureStyles()
        {
            if (!skinned && overlayStyle != null && GameSkin.TryLoad()) ApplyGameSkin();
            if (overlayStyle != null) return;
            darkTex = new Texture2D(1, 1);
            darkTex.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.94f));
            darkTex.Apply();

            windowStyle = new GUIStyle(GUI.skin.window) { fontSize = 16, fontStyle = FontStyle.Bold };
            windowStyle.normal.background = darkTex;
            windowStyle.onNormal.background = darkTex;
            windowStyle.normal.textColor = Color.white;
            windowStyle.onNormal.textColor = Color.white;

            overlayStyle = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
            overlayStyle.normal.background = darkTex;
            overlayStyle.normal.textColor = new Color(0.6f, 1f, 0.6f);

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            labelStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f);
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.82f, 0.45f);
            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            valueStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            arrowStyle = buttonStyle;
            if (GameSkin.TryLoad()) ApplyGameSkin();
        }

        // Optik des Spiels: Schiefer-Hintergrund, Eisenrahmen, Holz-Ueberschriften, rote Buttons, Pergament-Farben
        private void ApplyGameSkin()
        {
            try
            {
                var cream = new Color(0.93f, 0.87f, 0.74f);
                var gold = new Color(1f, 0.86f, 0.55f);
                var pad = new RectOffset(10, 10, 4, 4);
                windowStyle = GameSkin.Box("body_table-bg_1", GUIStyle.none, 16, cream);
                windowStyle.padding = new RectOffset(38, 38, 34, 30);
                windowStyle.onNormal = windowStyle.normal;
                frameStyle = GameSkin.Box("comm-frame_1-border", GUIStyle.none, 0, cream);
                titleStyle = GameSkin.Box("main_window-header_1", GUIStyle.none, 20, new Color(0.25f, 0.14f, 0.07f));
                titleStyle.alignment = TextAnchor.MiddleCenter; titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fixedHeight = 44; titleStyle.margin = new RectOffset(0, 0, 0, 8);
                headerStyle = GameSkin.Box("craft_window-craft_plate", GUIStyle.none, 16, gold);
                headerStyle.fontStyle = FontStyle.Bold; headerStyle.padding = new RectOffset(14, 10, 7, 7); headerStyle.margin = new RectOffset(0, 12, 6, 4);
                buttonStyle = GameSkin.Button("comm-btn-simple_red", GUIStyle.none, 15, cream);
                buttonStyle.alignment = TextAnchor.MiddleCenter; buttonStyle.padding = pad; buttonStyle.margin = new RectOffset(3, 3, 3, 3); buttonStyle.fixedHeight = 34;
                arrowStyle = GameSkin.Button("comm-btn-small_grey", GUIStyle.none, 16, cream);
                arrowStyle.alignment = TextAnchor.MiddleCenter; arrowStyle.margin = new RectOffset(3, 3, 3, 3); arrowStyle.fixedHeight = 34;
                valueStyle = GameSkin.Box("comm-value_frame_2", GUIStyle.none, 15, cream);
                valueStyle.alignment = TextAnchor.MiddleCenter; valueStyle.padding = pad; valueStyle.margin = new RectOffset(3, 3, 3, 3); valueStyle.fixedHeight = 34;
                labelStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft, fixedHeight = 34 };
                labelStyle.normal.textColor = cream;
                smallStyle.normal.textColor = new Color(0.78f, 0.74f, 0.66f);
                overlayStyle = GameSkin.Box("comm-value_frame_2", GUIStyle.none, 16, new Color(0.72f, 0.95f, 0.6f));
                overlayStyle.alignment = TextAnchor.MiddleLeft; overlayStyle.padding = new RectOffset(12, 12, 4, 4);
                vbarStyle = GameSkin.Box("craft_window-craft_plate", GUIStyle.none, 0, cream);
                vbarStyle.fixedWidth = 18; vbarStyle.margin = new RectOffset(6, 0, 0, 0);
                vthumbStyle = GameSkin.Box("comm-btn-small_grey-active", GUIStyle.none, 0, cream);
                vthumbStyle.fixedWidth = 18;
                var thin = new RectOffset(9, 9, 9, 9);
                foreach (GUIStyle st in new[] { buttonStyle, arrowStyle, valueStyle, overlayStyle, vthumbStyle }) st.border = thin;
                vbarStyle.border = new RectOffset(6, 6, 6, 6);
                if (GameSkin.PixelFont != null) foreach (GUIStyle st in new[] { labelStyle, smallStyle }) st.font = GameSkin.PixelFont;
                skinned = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Skin: " + e.Message); }
        }
    }
}
