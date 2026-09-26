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
    internal sealed class TweaksGui : MonoBehaviour
    {
        private const int WindowId = 0x6B2D, WeekWindowId = 0x6B2E, NewsWindowId = 0x6B2F;
        private bool newsOpen, newsSinceUpdate;
        private Rect newsWin;
        private Vector2 newsScroll;
        private GUIStyle newsStyle;

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
            RunPendingReopen();
            if (menuOpen && capturingKey == null && Input.GetKeyDown(KeyCode.Escape)) SetMenu(false);
            else if (newsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseNews();
            UpdateEnabled();
            OverlayStats.Configure(Plugin.ShowOverlay.Value, Plugin.OvCpu.Value, Plugin.OvGpu.Value, Plugin.OvRam.Value, Plugin.OvVram.Value, Plugin.OvFrameTime.Value);
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
            var parts = new System.Collections.Generic.List<string>();
            const float GB = 1024f * 1024f * 1024f;
            if (Plugin.OvFps.Value) parts.Add($"{r.AvgFps:0} FPS");
            if (Plugin.OvLows.Value) parts.Add($"1%: {r.Low1Fps:0}");
            if (Plugin.OvFrameTime.Value) parts.Add(r.AvgFps > 0 ? $"{1000f / r.AvgFps:0.0} ms (max {r.MaxMs:0})" : "- ms");
            if (Plugin.OvCpu.Value) parts.Add(OverlayStats.CpuPercent >= 0 ? $"CPU {OverlayStats.CpuPercent:0} %" : "CPU …");
            if (Plugin.OvGpu.Value)
            {
                if (OverlayStats.GpuPercent >= 0) parts.Add($"GPU {OverlayStats.GpuPercent:0} %");
                else if (OverlayStats.GpuFrameMs > 0) parts.Add($"GPU {OverlayStats.GpuFrameMs:0.0} ms");
                else parts.Add("GPU n/a");
            }
            if (Plugin.OvRam.Value)
            {
                long ram = OverlayStats.RamUsed;
                parts.Add(ram > 0 ? $"RAM {ram / GB:0.0}/{SystemInfo.systemMemorySize / 1024f:0} GB" : "RAM …");
            }
            if (Plugin.OvVram.Value)
            {
                long v = OverlayStats.VramBytes;
                long budget = System.Threading.Interlocked.Read(ref OverlayStats.VramBudget);
                int total = SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize : (int)(budget / (1024 * 1024));
                parts.Add(v > 0 ? (total > 0 ? $"VRAM {v / GB:0.0}/{total / 1024f:0} GB" : $"VRAM {v / GB:0.0} GB") : "VRAM n/a");
            }
            if (Plugin.OvResolution.Value) parts.Add($"{Screen.width}x{Screen.height}");
            if (Plugin.OvClock.Value) parts.Add(DateTime.Now.ToString("HH:mm"));
            if ((Plugin.OvWeekday.Value || Plugin.OvGameTime.Value) && WeekPlan.InGame)
            {
                try
                {
                    var env = MainGame.Instance.GameSave.environmentData;
                    string g = "";
                    if (Plugin.OvWeekday.Value) { string id = WeekPlan.IdForNumber(env.CurrentDayNumber); if (id != null) g = WeekPlan.DayName(id); }
                    if (Plugin.OvGameTime.Value)
                    {
                        // Tageszeit 0..1 = 0..24 Uhr (0,25 Sonnenaufgang, 0,5 Mittag); auf 10 Minuten gerundet wie eine Spieluhr
                        int min = Mathf.FloorToInt(Mathf.Repeat(env.TimeOfDay, 1f) * 1440f) / 10 * 10;
                        g += (g.Length > 0 ? " " : "") + (min / 60).ToString("00") + ":" + (min % 60).ToString("00");
                    }
                    if (g.Length > 0) parts.Add(g);
                }
                catch { }
            }
            if (parts.Count == 0) return "";
            return string.Join(Plugin.OvLayout.Value == "Column" ? "\n" : "   ", parts.ToArray());
        }

        // oben rechts steht im Spiel der Gebietsname - im Spiel darunter anfangen
        private static float TopFor(string corner) => corner == "TopRight" && WeekPlan.InGame ? 66f : 12f;

        private Rect DrawOverlay(float scale)
        {
            if (string.IsNullOrEmpty(overlayText) || HudToggle.Hidden) return Rect.zero;
            var content = new GUIContent(overlayText);
            Vector2 size = overlayStyle.CalcSize(content);
            size.x += 4;
            float w = Screen.width / scale, h = Screen.height / scale, m = 12f;
            string c = Plugin.OvCorner.Value;
            float x = c.EndsWith("Right") ? w - size.x - m : m;
            float y = c.StartsWith("Top") ? TopFor(c) : h - size.y - m;
            var r = new Rect(x, y, size.x, size.y);
            GUI.Label(r, content, overlayStyle);
            return r;
        }

        // ---------- Pin-Liste ----------
        private GUIStyle pinTitleStyle, pinRowStyle, pinBoxStyle, pinXStyle;
        private string pinStyleSize;

        private void EnsurePinStyles()
        {
            string size = Plugin.PinsSize.Value;
            if (pinTitleStyle != null && pinStyleSize == size) return;
            pinStyleSize = size;
            int f = size == "Small" ? 15 : size == "Large" ? 22 : size == "ExtraLarge" ? 26 : 18;
            pinBoxStyle = new GUIStyle(overlayStyle) { padding = new RectOffset(12, 12, 8, 8), fixedHeight = 0, fixedWidth = 0 };
            pinTitleStyle = new GUIStyle(labelStyle) { fontSize = f + 1, fontStyle = FontStyle.Bold, fixedHeight = 0, wordWrap = false, alignment = TextAnchor.MiddleLeft, richText = true };
            pinTitleStyle.normal.textColor = new Color(1f, 0.86f, 0.55f);
            pinRowStyle = new GUIStyle(labelStyle) { fontSize = f, fixedHeight = 0, wordWrap = false, alignment = TextAnchor.MiddleLeft, richText = true };
            pinXStyle = new GUIStyle(pinRowStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            pinXStyle.normal.textColor = new Color(0.85f, 0.6f, 0.5f);
            pinXStyle.hover.textColor = Color.white;
        }

        private void DrawPins(float scale, Rect ov)
        {
            EnsurePinStyles();
            float line = pinRowStyle.fontSize + 10f, icon = line - 2f, w = 0f, h = 0f;
            // Breite und Hoehe vorab berechnen
            foreach (Pins.Pin p in Pins.List)
            {
                w = Mathf.Max(w, pinTitleStyle.CalcSize(new GUIContent(p.Title + Labels.T("  bereit", "  ready"))).x + icon + 34f);
                h += line + 4f;
                foreach (Pins.Need n in p.Needs)
                {
                    w = Mathf.Max(w, pinRowStyle.CalcSize(new GUIContent(n.Name + "   " + n.Have + " / " + n.Count)).x + icon + 22f);
                    h += line;
                }
                h += 6f;
            }
            w += 24f; h += 12f;
            float sw = Screen.width / scale, sh = Screen.height / scale, m = 12f;
            string c = Plugin.PinsCorner.Value;
            float x = c.EndsWith("Right") ? sw - w - m : m;
            float y;
            if (c.StartsWith("Top")) y = ov.height > 0 ? ov.yMax + 6f : TopFor(c);
            else y = ov.height > 0 ? ov.y - 6f - h : sh - h - m;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, pinBoxStyle);
            float cy = y + 6f, cx = x + 12f;
            Pins.Pin remove = null;
            foreach (Pins.Pin p in Pins.List)
            {
                Texture2D ti = Pins.Icon(p.IconId);
                if (ti != null) GUI.DrawTexture(new Rect(cx, cy, icon, icon), ti, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(cx + icon + 6f, cy, w - icon - 50f, line), p.Title + (p.Ready ? "  <color=#9be27f>" + Labels.T("bereit", "ready") + "</color>" : ""), pinTitleStyle);
                if (GUI.Button(new Rect(x + w - 30f, cy, 22f, line), new GUIContent("x", Labels.T("Loslösen", "Unpin")), pinXStyle)) remove = p;
                cy += line + 4f;
                foreach (Pins.Need n in p.Needs)
                {
                    Texture2D ni = Pins.Icon(n.IconId);
                    if (ni != null) GUI.DrawTexture(new Rect(cx + 10f, cy + 1f, icon - 2f, icon - 2f), ni, ScaleMode.ScaleToFit);
                    string col = n.Have >= n.Count ? "#9be27f" : "#ff7a6a";
                    GUI.Label(new Rect(cx + icon + 14f, cy, w - icon - 30f, line), n.Name + "   <color=" + col + ">" + n.Have + " / " + n.Count + "</color>", pinRowStyle);
                    cy += line;
                }
                cy += 6f;
            }
            if (remove != null) Pins.Unpin(remove);
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
            SetBlocker(menuOpen || newsOpen);
            if (weekOpen && !WeekPlan.InGame) weekOpen = false;
            bool want = menuOpen || weekOpen || newsOpen || Plugin.ShowOverlay.Value || ManualSave.ShowMessage || GraphicsBench.Running || (Pins.List.Count > 0 && Plugin.PinsEnabled.Value);
            if (enabled != want) enabled = want;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (capturingKey != null && Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None)
            {
                if (Event.current.keyCode != KeyCode.Escape) capturingKey.Value = new KeyboardShortcut(Event.current.keyCode);
                else if (capturingKey != Plugin.MenuKey) capturingKey.Value = KeyboardShortcut.Empty;
                capturingKey = null;
                Event.current.Use();
            }

            GUIStyle oldThumb = GUI.skin.verticalScrollbarThumb;
            if (skinned) GUI.skin.verticalScrollbarThumb = vthumbStyle;
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            Rect ov = Rect.zero;
            if (Plugin.ShowOverlay.Value) ov = DrawOverlay(scale);
            if (Pins.List.Count > 0 && Plugin.PinsEnabled.Value && WeekPlan.InGame && !HudToggle.Hidden)
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
            GUI.matrix = old;
            GUI.skin.verticalScrollbarThumb = oldThumb;
        }

        private void DrawWindow(int id)
        {
            if (skinned) GUILayout.Label("GK2 Vanilla+  ·  by McFly7", titleStyle);
            if (UpdateCheck.Available) DrawUpdate();
            GUILayout.Label(fpsText + "     " + SystemInfo.graphicsDeviceVersion, labelStyle);
            GUILayout.Label(Labels.T("Aktiv: ", "Active: ") + Plugin.DescribeFeatures(), smallStyle);
            GUILayout.Space(4);

            scroll = skinned ? GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, vbarStyle, GUIStyle.none, GUILayout.Height(560))
                             : GUILayout.BeginScrollView(scroll, GUILayout.Height(560));
            Header(Labels.T("Spiel", "Game"));
            DrawGameTier();

            DrawGfxBench();

            Header(Labels.T("Bildrate", "Frame rate"));
            DrawEntry(Plugin.Pacing);
            DrawEntry(Plugin.TargetFps);

            Header(Labels.T("Grafik (überschreibt einzelne Teile der Grafikstufe)", "Graphics (overrides parts of the graphics tier)"));
            DrawEntry(Plugin.RenderMode);
            DrawEntry(Plugin.Shadows);
            DrawEntry(Plugin.Hbao);
            DrawEntry(Plugin.PointLights);
            DrawEntry(Plugin.BackLight);
            DrawEntry(Plugin.Water);
            DrawEntry(Plugin.Clouds);

            Header(Labels.T("Komfort", "Comfort"));
            DrawEntry(Plugin.Zoom);
            DrawEntry(Plugin.PauseInBackground);
            DrawEntry(Plugin.AutoSaveMinutes);
            DrawEntry(Plugin.SkipIntro);
            DrawEntry(Plugin.MenuExtend);
            DrawEntry(Plugin.MenuModdedLabel);
            DrawEntry(Plugin.GameMenuButton);
            DrawEntry(Plugin.WeekPlanNotify);

            DrawBackups();

            Header(Labels.T("Leistung", "Performance"));
            DrawEntry(Plugin.PhysicsHz);
            DrawEntry(Plugin.GameLog);

            Header(Labels.T("Anzeige", "Interface"));
            DrawEntry(Plugin.Language);
            DrawEntry(Plugin.StatsLogSeconds);
            DrawEntry(Plugin.MenuKey);
            DrawEntry(Plugin.OverlayKey);
            DrawEntry(Plugin.SaveKey);
            DrawEntry(Plugin.WeekPlanKey);
            DrawEntry(Plugin.HudKey);
#if !NEXUS
            DrawEntry(Plugin.CheckUpdates);
#endif

            Header(Labels.T("Anpinnen", "Pinning"));
            DrawEntry(Plugin.PinsEnabled);
            DrawEntry(Plugin.PinsCorner);
            DrawEntry(Plugin.PinsSize);
            if (Pins.List.Count > 0 && GUILayout.Button(Labels.T("Alle Pins entfernen", "Remove all pins"), buttonStyle, GUILayout.Width(260))) { Pins.List.Clear(); PinButton.RefreshAll(); }

            Header(Labels.T("FPS-Anzeige", "FPS display"));
            foreach (ConfigEntryBase e in new ConfigEntryBase[] { Plugin.ShowOverlay, Plugin.OvCorner, Plugin.OvLayout, Plugin.OvFps, Plugin.OvLows,
                         Plugin.OvFrameTime, Plugin.OvCpu, Plugin.OvGpu, Plugin.OvRam, Plugin.OvVram, Plugin.OvResolution, Plugin.OvClock, Plugin.OvWeekday, Plugin.OvGameTime })
                DrawEntry(e);

            if (WineFix.IsWine) DrawWineFix();

            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            {
                if (info.Instance == null || info.Instance == Plugin.Instance) continue;
                ConfigFile cfg = info.Instance.Config;
                if (cfg == null || cfg.Count == 0) continue;
                Header(info.Metadata.Name + Labels.T(" (wirkt nach Neustart)", " (applies after restart)"));
                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in cfg) DrawEntry(kv.Value);
            }
            GUILayout.EndScrollView();
            if (scrollToBench && Event.current.type == EventType.Repaint) { scroll.y = Mathf.Max(0, benchY - 10); scrollToBench = false; }
            if (pendingScrollHeader != null && Event.current.type == EventType.Repaint && headerY.TryGetValue(pendingScrollHeader, out float hy)) { scroll.y = Mathf.Max(0, hy - 10); pendingScrollHeader = null; }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Labels.T("Jetzt speichern", "Save now"), buttonStyle)) ManualSave.Save(true);
            if (GUILayout.Button(Labels.T("Was ist neu?", "What's new?"), buttonStyle)) ShowNews(false);
            if (GUILayout.Button(Labels.T("Grafik zurücksetzen", "Reset graphics"), buttonStyle)) ResetTweaks();
            if (GUILayout.Button(Labels.T("Schließen (", "Close (") + Plugin.MenuKey.Value + ")", buttonStyle)) SetMenu(false);
            GUILayout.EndHorizontal();
            string foot = ManualSave.ShowMessage ? ManualSave.Message : GUI.tooltip;
            GUILayout.Label(string.IsNullOrEmpty(foot) ? " " : foot, smallStyle, GUILayout.Height(36));
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, win.width, win.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

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
            if (GUILayout.Button(on ? Labels.T("An", "On") : Labels.T("Aus", "Off"), buttonStyle, GUILayout.Width(310))) WineFix.Enabled = !on;
            GUILayout.EndHorizontal();
            if (WineFix.Pending)
                GUILayout.Label(Labels.T("Wirkt nach Neustart: Spiel UND Steam beenden (bzw. die CrossOver-Flasche neu starten).",
                    "Applies after a restart: quit the game AND Steam (or restart the CrossOver bottle)."), smallStyle);
        }

        // ---------- Grafik-Benchmark ----------
        private bool scrollToBench;
        private float benchY;

        internal void ShowBenchResults() => scrollToBench = true;

        private void DrawGfxBench()
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Grafik-Benchmark", "Graphics benchmark"), Labels.T(
                "Geht alle Grafikstufen nacheinander durch (je ca. 13 s, insgesamt gut 1 Minute) und misst die FPS ohne Limit. Am Ende gibt es einen Score und eine Empfehlung. Nichts wird gespeichert, deine Einstellungen bleiben wie sie sind. Am besten an einer typischen Stelle stehen bleiben. Esc bricht ab.",
                "Runs through all graphics tiers (about 13 s each, a bit over a minute in total) and measures FPS without a limit. You get a score and a recommendation at the end. Nothing is saved, your settings stay as they are. Best to stand still at a typical spot. Esc cancels.")),
                labelStyle, GUILayout.Width(268));
            GUI.enabled = GraphicsBench.CanRun;
            if (GUILayout.Button(GraphicsBench.CanRun ? Labels.T("Benchmark starten", "Start benchmark") : Labels.T("nur im laufenden Spiel", "only while playing"), buttonStyle, GUILayout.Width(310)))
                GraphicsBench.Start();
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
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (newsSinceUpdate && GUILayout.Button(Labels.T("Alle Versionen", "All versions"), buttonStyle)) { newsSinceUpdate = false; newsScroll = Vector2.zero; }
            if (GUILayout.Button(Labels.T("Schließen", "Close"), buttonStyle)) CloseNews();
            GUILayout.EndHorizontal();
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, newsWin.width, newsWin.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        // ---------- Wochenplan (F6) ----------
        private void DrawWeekWindow(int id)
        {
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
            if (GUILayout.Button(Labels.T("Schließen (", "Close (") + Plugin.WeekPlanKey.Value + ")", buttonStyle)) { weekOpen = false; UpdateEnabled(); }
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, weekWin.width, weekWin.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        // ---------- Spielstand-Backups ----------
        private void DrawBackups()
        {
            Header(Labels.T("Spielstand-Backups", "Save backups"));
            DrawEntry(Plugin.BackupCount);
            DrawEntry(Plugin.BackupMinutes);
            if (Time.realtimeSinceStartup > backupListAt) { backupList = Backups.List(); backupListAt = Time.realtimeSinceStartup + 3f; }
            bool inMenu = MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.MainMenu;
            if (backupList.Count == 0) GUILayout.Label(Labels.T("Noch keine Backups vorhanden.", "No backups yet."), smallStyle);
            foreach (Backups.Item b in backupList)
            {
                GUILayout.BeginHorizontal();
                string when = b.Time == default(System.DateTime) ? System.IO.Path.GetFileName(b.Dir) : b.Time.ToString(Labels.German ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
                GUILayout.Label(when + "   " + b.Slot + "   " + (b.Bytes / 1048576f).ToString("0.0") + " MB" + (b.Dir.EndsWith("_restore") ? Labels.T("  (vor Wiederherstellung)", "  (before restore)") : ""), labelStyle, GUILayout.Width(460));
                if (inMenu)
                {
                    bool confirm = confirmRestore == b;
                    if (GUILayout.Button(confirm ? Labels.T("Sicher?", "Sure?") : Labels.T("Laden", "Restore"), buttonStyle, GUILayout.Width(110)))
                    {
                        if (!confirm) confirmRestore = b;
                        else { Backups.Restore(b, out backupMsg); confirmRestore = null; backupListAt = 0; }
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(!string.IsNullOrEmpty(backupMsg) ? backupMsg : (inMenu ? Labels.T("\"Laden\" ersetzt den Spielstand durch das Backup (der aktuelle Stand wird vorher gesichert).", "\"Restore\" replaces the save with the backup (the current save is backed up first).")
                : Labels.T("Wiederherstellen ist im Hauptmenü möglich.", "Restoring is available in the main menu.")), smallStyle);
            if (GUILayout.Button(Labels.T("Backup-Ordner öffnen", "Open backup folder"), buttonStyle, GUILayout.Width(260)))
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
                if (GUILayout.Button(new GUIContent(Labels.T("Speichern & aktualisieren", "Save & update"),
                        Labels.T("Speichert (falls im Spiel), beendet das Spiel und installiert die neue Version. Danach kann das Spiel direkt neu gestartet werden.",
                                 "Saves (if in game), quits the game and installs the new version. The game can be restarted right after.")), buttonStyle))
                    UpdateCheck.SaveAndUpdate(true);
            }
            if (GUILayout.Button(Labels.T("Download-Seite", "Download page"), buttonStyle, GUILayout.Width(150))) Application.OpenURL(UpdateCheck.ReleasePage);
            GUILayout.EndHorizontal();
        }

        private readonly Dictionary<string, float> headerY = new Dictionary<string, float>();
        private string pendingScrollHeader;

        internal void ScrollToHeader(string text) => pendingScrollHeader = text;

        private void Header(string text)
        {
            GUILayout.Space(8);
            GUILayout.Label(text, headerStyle);
            if (Event.current.type == EventType.Repaint) headerY[text] = GUILayoutUtility.GetLastRect().y;
        }

        private void DrawGameTier()
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Grafikstufe", "Graphics tier"), Labels.T("Die Stufe aus dem Spielmenü. Die Werte unter 'Grafik' überschreiben einzelne Teile davon.", "The tier from the game menu. The values under 'Graphics' override parts of it.")), labelStyle, GUILayout.Width(268));
            var tiers = (GraphicsTier[])Enum.GetValues(typeof(GraphicsTier));
            int i = Math.Max(0, Array.IndexOf(tiers, gs.graphicsTier));
            if (GUILayout.Button("<", arrowStyle, GUILayout.Width(36))) SetTier(tiers[(i - 1 + tiers.Length) % tiers.Length]);
            GUILayout.Label(Labels.Tier(gs.graphicsTier), valueStyle, GUILayout.Width(230));
            if (GUILayout.Button(">", arrowStyle, GUILayout.Width(36))) SetTier(tiers[(i + 1) % tiers.Length]);
            GUILayout.EndHorizontal();
        }

        private static void SetTier(GraphicsTier tier)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            gs.graphicsTier = tier;
            try { gs.ApplyGraphicsTier(applySave: true); } catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
        }

        private void DrawEntry(ConfigEntryBase e)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.Name(e), Labels.Tip(e)), labelStyle, GUILayout.Width(268));
            AcceptableValueBase acc = e.Description?.AcceptableValues;
            if (e.SettingType == typeof(bool))
            {
                bool v = (bool)e.BoxedValue;
                if (GUILayout.Button(v ? Labels.T("An", "On") : Labels.T("Aus", "Off"), buttonStyle, GUILayout.Width(310))) e.BoxedValue = !v;
            }
            else if (acc is AcceptableValueList<string> ls) Cycle(e, ls.AcceptableValues.Cast<object>().ToArray());
            else if (acc is AcceptableValueList<int> li) Cycle(e, li.AcceptableValues.Cast<object>().ToArray());
            else if (e.SettingType == typeof(KeyboardShortcut)) KeyField((ConfigEntry<KeyboardShortcut>)e);
            else TextField(e);
            GUILayout.EndHorizontal();
        }

        private void Cycle(ConfigEntryBase e, object[] values)
        {
            int i = Math.Max(0, Array.IndexOf(values, e.BoxedValue));
            if (GUILayout.Button("<", arrowStyle, GUILayout.Width(36))) e.BoxedValue = values[(i - 1 + values.Length) % values.Length];
            GUILayout.Label(Labels.Value(e, values[i]), valueStyle, GUILayout.Width(230));
            if (GUILayout.Button(">", arrowStyle, GUILayout.Width(36))) e.BoxedValue = values[(i + 1) % values.Length];
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
