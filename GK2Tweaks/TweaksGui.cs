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
        private const int WindowId = 0x6B2D;
        private bool menuOpen;
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
        internal void ScrollToEnd() => scroll.y = 100000f;

        internal void ToggleMenu() => SetMenu(!menuOpen);

        internal void SetMenu(bool open)
        {
            if (menuOpen == open) return;
            menuOpen = open;
            capturingKey = null;
            // Spielersteuerung sperren, damit Klicks im Menue nicht im Spiel landen
            try { MainGame.PlayerController?.SetControlTakenType(TakenControlType.ByTeleport, !open); } catch { }
            UpdateEnabled();
        }

        internal void Tick(float dt)
        {
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
            if (parts.Count == 0) return "";
            return string.Join(Plugin.OvLayout.Value == "Column" ? "\n" : "   ", parts.ToArray());
        }

        private void DrawOverlay(float scale)
        {
            if (string.IsNullOrEmpty(overlayText)) return;
            var content = new GUIContent(overlayText);
            Vector2 size = overlayStyle.CalcSize(content);
            size.x += 4;
            float w = Screen.width / scale, h = Screen.height / scale, m = 12f;
            string c = Plugin.OvCorner.Value;
            float x = c.EndsWith("Right") ? w - size.x - m : m;
            float y = c.StartsWith("Top") ? m : h - size.y - m;
            GUI.Label(new Rect(x, y, size.x, size.y), content, overlayStyle);
        }

        private void UpdateEnabled()
        {
            bool want = menuOpen || Plugin.ShowOverlay.Value || ManualSave.ShowMessage;
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
            if (Plugin.ShowOverlay.Value) DrawOverlay(scale);
            if (ManualSave.ShowMessage && !menuOpen)
            {
                var msg = new GUIContent(ManualSave.Message);
                Vector2 sz = overlayStyle.CalcSize(msg);
                GUI.Label(new Rect((Screen.width / scale - sz.x) / 2f, 24, sz.x + 4, sz.y), msg, overlayStyle);
            }
            if (menuOpen) win = GUILayout.Window(WindowId, win, DrawWindow, skinned ? "" : "GK2 Vanilla+ · by McFly7", windowStyle);
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

            Header(Labels.T("Leistung", "Performance"));
            DrawEntry(Plugin.PhysicsHz);
            DrawEntry(Plugin.GameLog);

            Header(Labels.T("Anzeige", "Interface"));
            DrawEntry(Plugin.Language);
            DrawEntry(Plugin.StatsLogSeconds);
            DrawEntry(Plugin.MenuKey);
            DrawEntry(Plugin.OverlayKey);
            DrawEntry(Plugin.SaveKey);
#if !NEXUS
            DrawEntry(Plugin.CheckUpdates);
#endif

            Header(Labels.T("FPS-Anzeige", "FPS display"));
            foreach (ConfigEntryBase e in new ConfigEntryBase[] { Plugin.ShowOverlay, Plugin.OvCorner, Plugin.OvLayout, Plugin.OvFps, Plugin.OvLows,
                         Plugin.OvFrameTime, Plugin.OvCpu, Plugin.OvGpu, Plugin.OvRam, Plugin.OvVram, Plugin.OvResolution, Plugin.OvClock })
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

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Labels.T("Jetzt speichern", "Save now"), buttonStyle)) ManualSave.Save(true);
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

        private void Header(string text)
        {
            GUILayout.Space(8);
            GUILayout.Label(text, headerStyle);
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
