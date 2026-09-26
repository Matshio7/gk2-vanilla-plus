#if DEV
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Messlauf: Hauptmenue abwarten -> "Fortsetzen" -> Laden abwarten -> Aufwaermen -> Varianten nacheinander messen -> Spiel beenden.
    // Speichern ist in diesem Modus blockiert (SaveBlockPatch). Alle geaenderten Einstellungen werden am Ende zurueckgesetzt.
    internal sealed class Benchmark
    {
        private enum Phase { WaitMenu, WaitLoad, Warmup, Settle, Measure, Done }

        private sealed class Variant
        {
            public string Name;
            public readonly List<KeyValuePair<string, string>> Sets = new List<KeyValuePair<string, string>>();
        }

        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "render", "Graphics.RenderMode" }, { "shadows", "Graphics.Shadows" }, { "hbao", "Graphics.AmbientOcclusion" },
            { "lights", "Graphics.PointLights" }, { "backlight", "Graphics.BackLight" }, { "water", "Graphics.Water" },
            { "clouds", "Graphics.Clouds" }, { "physics", "Performance.PhysicsHz" }, { "log", "Performance.GameLog" },
            { "pacing", "FrameRate.Mode" }, { "fps", "FrameRate.TargetFps" },
        };

        private Phase phase = Phase.WaitMenu;
        private float phaseStart;
        private float menuSince = -1f;
        private float nextPoll;
        private readonly FrameStats stats = new FrameStats();
        private int shotStep;
        private bool mainShot;
        private int gcStart;
        private readonly List<Variant> variants = new List<Variant>();
        private int variantIndex;
        private readonly Dictionary<ConfigEntryBase, string> baseline = new Dictionary<ConfigEntryBase, string>();
        private GraphicsTier? baseTier;
        private ResolutionConfig baseRes;

        public Benchmark()
        {
            phaseStart = Time.realtimeSinceStartup;
            ParseVariants(Plugin.BenchVariants.Value);
            // Test-Pins vom letzten Lauf nicht in neue Screenshots uebernehmen
            try { System.IO.File.Delete(Path.Combine(Paths.BepInExRootPath, "GK2VanillaPlus", "pins_bench.txt")); } catch { }
            Plugin.Log.LogInfo("[BENCH] START label=" + Plugin.BenchLabel.Value + " variants=" + variants.Count);
        }

        private void ParseVariants(string spec)
        {
            foreach (string part in (spec ?? "").Split(';'))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                var v = new Variant { Name = p.Replace(",", "+").Replace("=", "-") };
                foreach (string kv in p.Split(','))
                {
                    int eq = kv.IndexOf('=');
                    if (eq <= 0) continue;
                    v.Sets.Add(new KeyValuePair<string, string>(kv.Substring(0, eq).Trim(), kv.Substring(eq + 1).Trim()));
                }
                if (p.Equals("base", StringComparison.OrdinalIgnoreCase)) v.Name = "base";
                variants.Add(v);
            }
            if (variants.Count == 0) variants.Add(new Variant { Name = "base" });
        }

        public void Update(float dt)
        {
            float now = Time.realtimeSinceStartup;
            float inPhase = now - phaseStart;
            switch (phase)
            {
                case Phase.WaitMenu:
                    if (inPhase > 300f) { Fail("Hauptmenue nicht erreicht"); return; }
                    if (now < nextPoll) return;
                    nextPoll = now + 0.5f;
                    MainGame mg = MainGame.Instance;
                    if (mg == null || mg.gameState != MainGame.GameState.MainMenu) return;
                    UIMainMenuWindow w = UnityEngine.Object.FindFirstObjectByType<UIMainMenuWindow>();
                    if (w == null || !w.IsShown || !w.IsContinueButtonWillBeActive()) { menuSince = -1f; return; }
                    if (menuSince < 0f)
                    {
                        menuSince = now;
                        if (!string.IsNullOrEmpty(Plugin.BenchShotRes.Value)) { try { SetResolution(Plugin.BenchShotRes.Value); } catch (Exception e) { Plugin.Log.LogWarning("[BENCH] res: " + e.Message); } }
                        if (Steam) Try(() => { origLang = GameSettings.Instance.language; SetGameLanguage("en"); TourSet(Plugin.Language, "English"); });
                        return;
                    }
                    if (Plugin.BenchMenuShot.Value && !mainShot && now - menuSince >= 5f) { Shot(Steam ? "s01_mainmenu" : "mainmenu"); mainShot = true; if (Features || Steam) Plugin.Instance.Gui.SetMenu(true); return; }
                    if ((Features || Steam) && mainShot && !savesShot && now - menuSince >= 8f) { Shot(Steam ? "s15_saves" : "f01_saves_mainmenu"); savesShot = true; return; }
                    if ((Features || Steam) && now - menuSince < 11f) { if (now - menuSince >= 10f) Plugin.Instance.Gui.SetMenu(false); return; }
                    if (now - menuSince < (Plugin.BenchMenuShot.Value ? 7f : 3f)) return;
                    Plugin.Log.LogInfo($"[BENCH] Hauptmenue nach {now:0.0}s, Fortsetzen");
                    w.OnContinueButtonClicked();
                    SetPhase(Phase.WaitLoad, now);
                    break;

                case Phase.WaitLoad:
                    if (inPhase > 360f) { Fail("Laden dauerte zu lange"); return; }
                    if (now < nextPoll) return;
                    nextPoll = now + 0.5f;
                    if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.InGame) return;
                    UILoadingOverlay overlay = LazyUI.Get<UILoadingOverlay>();
                    if (overlay != null && overlay.IsShown) return;
                    Plugin.Log.LogInfo($"[BENCH] geladen nach {inPhase:0.0}s");
                    try { LoopProfiler.Install(); } catch (Exception e) { Plugin.Log.LogWarning("LoopProfiler: " + e.Message); }
                    SetPhase(Phase.Warmup, now);
                    break;

                case Phase.Warmup:
                    if (Plugin.BenchMenuShot.Value) { if (Steam) SteamTour(inPhase); else if (Features) FeatureTour(inPhase); else MenuShot(inPhase); }
                    if (inPhase >= Plugin.BenchWarmup.Value)
                    {
                        if (Plugin.Instance.Gui.MenuOpen) Plugin.Instance.Gui.SetMenu(false);
                        variantIndex = 0;
                        ApplyVariant(variants[0]);
                        SetPhase(Phase.Settle, now);
                    }
                    break;

                case Phase.Settle:
                    if (inPhase >= 6f)
                    {
                        stats.Reset();
                        LoopProfiler.Reset();
                        SystemProfiler.Reset();
                        LoopProfiler.Active = true;
                        SystemProfiler.Active = true;
                        gcStart = GC.CollectionCount(0);
                        Plugin.Log.LogInfo("[BENCH] MESSUNG " + variants[variantIndex].Name + " " + Plugin.DescribeState());
                        SetPhase(Phase.Measure, now);
                    }
                    break;

                case Phase.Measure:
                    stats.Add(dt);
                    if (inPhase >= Plugin.BenchMeasure.Value)
                    {
                        Report(variants[variantIndex].Name);
                        variantIndex++;
                        if (variantIndex < variants.Count)
                        {
                            ApplyVariant(variants[variantIndex]);
                            SetPhase(Phase.Settle, now);
                        }
                        else
                        {
                            RestoreBaseline();
                            SetPhase(Phase.Done, now);
                            Plugin.Log.LogInfo("[BENCH] ENDE");
                            Application.Quit();
                        }
                    }
                    break;
            }
        }

        private void ApplyVariant(Variant v)
        {
            RestoreBaseline(keepSnapshots: true);
            foreach (KeyValuePair<string, string> kv in v.Sets)
            {
                try
                {
                    string key = kv.Key;
                    if (key.Equals("tier", StringComparison.OrdinalIgnoreCase)) { SetTier(kv.Value); continue; }
                    if (key.Equals("res", StringComparison.OrdinalIgnoreCase)) { SetResolution(kv.Value); continue; }
                    if (Aliases.TryGetValue(key, out string full)) key = full;
                    int dot = key.IndexOf('.');
                    ConfigEntryBase entry = FindEntry(key.Substring(0, dot), key.Substring(dot + 1));
                    if (entry == null) throw new Exception("unbekannte Einstellung");
                    if (!baseline.ContainsKey(entry)) baseline[entry] = entry.GetSerializedValue();
                    entry.SetSerializedValue(kv.Value);
                }
                catch (Exception e) { Plugin.Log.LogWarning("[BENCH] Variante " + v.Name + ": " + kv.Key + " -> " + e.Message); }
            }
        }

        private static ConfigEntryBase FindEntry(string section, string key)
        {
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in Plugin.Instance.Config)
                if (kv.Key.Section == section && kv.Key.Key == key) return kv.Value;
            return null;
        }

        private void SetTier(string value)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            if (baseTier == null) baseTier = gs.graphicsTier;
            gs.graphicsTier = (GraphicsTier)Enum.Parse(typeof(GraphicsTier), value, true);
            gs.ApplyGraphicsTier(applySave: false);
        }

        private void SetResolution(string value)
        {
            GameSettings gs = GameSettings.Instance;
            if (gs == null) return;
            string[] wh = value.ToLowerInvariant().Split('x');
            if (baseRes == null && gs.resolutionConfig != null) baseRes = gs.resolutionConfig.Copy();
            gs.resolutionConfig = new ResolutionConfig(int.Parse(wh[0]), int.Parse(wh[1]));
            gs.ApplyGraphicSettings(applySave: false);
        }

        private void RestoreBaseline(bool keepSnapshots = false)
        {
            foreach (KeyValuePair<ConfigEntryBase, string> kv in baseline)
            {
                try { if (kv.Key.GetSerializedValue() != kv.Value) kv.Key.SetSerializedValue(kv.Value); }
                catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
            }
            GameSettings gs = GameSettings.Instance;
            if (gs != null && baseTier != null && gs.graphicsTier != baseTier.Value)
            {
                gs.graphicsTier = baseTier.Value;
                gs.ApplyGraphicsTier(applySave: !keepSnapshots);
            }
            if (gs != null && baseRes != null && (gs.resolutionConfig == null || gs.resolutionConfig.ListedWidth != baseRes.ListedWidth || gs.resolutionConfig.ListedHeight != baseRes.ListedHeight))
            {
                gs.resolutionConfig = baseRes.Copy();
                gs.ApplyGraphicSettings(applySave: !keepSnapshots);
            }
            if (!keepSnapshots && gs != null) SaveSystem.SaveGameSettings();
        }

        // Screenshot-Tour fuer Workshop/GitHub: sauberes Bild, mit FPS-Anzeige, mit Mod-Menue
        private void MenuShot(float inPhase)
        {
            if (shotStep == 0 && inPhase > 10f) { Shot("gameplay"); shotStep = 1; }
            else if (shotStep == 1 && inPhase > 12f) { Plugin.ShowOverlay.Value = true; shotStep = 2; }
            else if (shotStep == 2 && inPhase > 15f) { Shot("overlay"); shotStep = 3; }
            else if (shotStep == 3 && inPhase > 17f) { Plugin.Instance.Gui.SetMenu(true); shotStep = 4; }
            else if (shotStep == 4 && inPhase > 20f) { Shot("modmenu"); shotStep = 5; }
            else if (shotStep == 5 && inPhase > 22f) { Plugin.Instance.Gui.SetMenu(false); Plugin.ShowOverlay.Value = false; shotStep = 6; }
            else if (shotStep == 6 && inPhase > 24f) { WeekPlan.DebugDump(); Plugin.Instance.Gui.ToggleWeekPlan(); shotStep = 7; }
            else if (shotStep == 7 && inPhase > 27f) { Shot("weekplan"); shotStep = 8; }
            else if (shotStep == 8 && inPhase > 29f) { Plugin.Instance.Gui.ToggleWeekPlan(); WeekPlan.OnNewDay(WeekPlan.TodayNumber); shotStep = 9; }
            else if (shotStep == 9 && inPhase > 31f) { Shot("notify"); shotStep = 10; }
            else if (shotStep == 10 && inPhase > 33f) { HudToggle.Toggle(); shotStep = 11; }
            else if (shotStep == 11 && inPhase > 35f) { Shot("nohud"); shotStep = 12; }
            else if (shotStep == 12 && inPhase > 37f) { HudToggle.Show(); Backups.BeforeSave(MainGame.Instance.SaveSlotData); Plugin.Log.LogInfo("[BENCH] backups: " + Backups.List().Count); shotStep = 13; }
            // 1.4.0: Pausenmenue mit Mods-Button, Rueckweg, Werkbank mit Pinnadel, Pin-Liste, Overlay mit Spielzeit
            else if (shotStep == 13 && inPhase > 39f) { Try(() => LazyUI.GetWindow<UIGamePauseWindow>().Open(null)); shotStep = 14; }
            else if (shotStep == 14 && inPhase > 42f) { Shot("pause"); shotStep = 15; }
            else if (shotStep == 15 && inPhase > 43f) { Try(() => ClickNamed("GK2VanillaPlus_Mods")); shotStep = 16; }
            else if (shotStep == 16 && inPhase > 46f) { Plugin.Log.LogInfo("[BENCH] menuOpen after Mods click: " + Plugin.Instance.Gui.MenuOpen); Shot("pause_mods"); shotStep = 17; }
            else if (shotStep == 17 && inPhase > 47f) { Plugin.Instance.Gui.SetMenu(false); shotStep = 18; }
            else if (shotStep == 18 && inPhase > 50f) { Plugin.Log.LogInfo("[BENCH] pause shown again: " + LazyUI.GetWindow<UIGamePauseWindow>().IsShown); Shot("pause_back"); Try(() => LazyUI.GetWindow<UIGamePauseWindow>().Close()); shotStep = 19; }
            else if (shotStep == 19 && inPhase > 52f) { Try(OpenNearestWorkbench); shotStep = 20; }
            else if (shotStep == 20 && inPhase > 55f) { Try(PinFirstRecipes); shotStep = 21; }
            else if (shotStep == 21 && inPhase > 57f) { Shot("craft_pin"); shotStep = 22; }
            else if (shotStep == 22 && inPhase > 58f) { Try(() => LazyUI.GetWindow<UICraftWindow>().Close()); Plugin.ShowOverlay.Value = true; Plugin.OvWeekday.Value = true; Plugin.OvGameTime.Value = true; shotStep = 23; }
            else if (shotStep == 23 && inPhase > 62f) { Plugin.Log.LogInfo("[BENCH] pins: " + Pins.List.Count); Shot("pins"); shotStep = 24; }
            else if (shotStep == 24 && inPhase > 63f) { Plugin.ShowOverlay.Value = false; Plugin.OvWeekday.Value = false; Plugin.OvGameTime.Value = false; Pins.List.Clear(); shotStep = 25; }
            else if (shotStep == 25 && inPhase > 65f) { GraphicsBench.Start(); shotStep = 26; }
            else if (shotStep == 26 && inPhase > 72f) { Shot("gfxbench_run"); shotStep = 27; }
            else if (shotStep == 27 && !GraphicsBench.Running && inPhase > 80f) { shotStep = 28; }
            else if (shotStep == 28 && inPhase > 140f) { Shot("gfxbench_result"); shotStep = 29; }
            else if (shotStep == 29 && inPhase > 142f) { Plugin.Instance.Gui.SetMenu(false); shotStep = 30; }
        }

        private static bool Steam => Plugin.BenchShotSet.Value == "steam";
        private static bool Features => Plugin.BenchShotSet.Value == "features";
        private bool savesShot;

        // Test der 1.4-Erweiterungen: Quest-Pin, Detailfenster-Pin, Pins speichern/laden, Zoom, Screenshot, Sprachen, Kontrast
        private void FeatureTour(float t)
        {
            TweaksGui gui = Plugin.Instance.Gui;
            switch (shotStep)
            {
                case 0: if (t > 8f) { Try(OpenSomeQuest); shotStep++; } break;
                case 1: if (t > 11f) { Try(() => { PinButton b = FindPin("quest:"); if (b != null) Pins.Toggle(b.Make()); Plugin.Log.LogInfo("[BENCH] quest pin button " + (b != null)); }); shotStep++; } break;
                case 2: if (t > 13f) { Shot("f02_quest_window"); shotStep++; } break;
                case 3: if (t > 14f) { Try(() => LazyUI.GetWindow<UIQuestInfoWindow>().Close()); Try(OpenNearestWorkbench); shotStep++; } break;
                case 4: if (t > 17f) { Try(OpenFirstRecipeDetail); shotStep++; } break;
                case 5: if (t > 20f) { Try(() => { PinButton b = FindPin("craft:", header: true); Plugin.Log.LogInfo("[BENCH] detail pin button " + (b != null)); if (b != null) Pins.Toggle(b.Make()); }); shotStep++; } break;
                case 6: if (t > 22f) { Shot("f03_recipe_detail"); shotStep++; } break;
                case 7: if (t > 23f) { Try(CloseCraftWindows); TourSet(Plugin.HighContrast, true); shotStep++; } break;
                case 8: if (t > 26f) { Shot("f04_pins_quest_highcontrast"); shotStep++; } break;
                case 9: if (t > 27f)
                    {
                        int before = Pins.List.Count;
                        Try(() => { PinStore.Save(); PinStore.Load(); });
                        Plugin.Log.LogInfo("[BENCH] pins save/load " + before + " -> " + Pins.List.Count + " : " + string.Join(" | ", Pins.List.ConvertAll(p => p.Title + (p.Note != null ? " [" + p.Note + "]" : "") + " " + p.Needs.Count).ToArray()));
                        TourSet(Plugin.HighContrast, false);
                        shotStep++;
                    } break;
                case 10: if (t > 28f) { Plugin.Log.LogInfo("[BENCH] zoom " + CameraZoom.Current); CameraZoom.TestSet(125f); shotStep++; } break;
                case 11: if (t > 31f) { Plugin.Log.LogInfo("[BENCH] zoom now " + CameraZoom.Current); Shot("f05_zoom125"); shotStep++; } break;
                case 12: if (t > 32f) { CameraZoom.TestSet(Plugin.Zoom.Value); Plugin.Instance.StartCoroutine(HiResShot.Take()); shotStep++; } break;
                case 13: if (t > 38f) { LogNewestShot(); TourSet(Plugin.Language, "Русский"); gui.SetMenu(true); shotStep++; } break;
                case 14: if (t > 41f) { Shot("f06_menu_ru"); shotStep++; } break;
                case 15: if (t > 42f) { TourSet(Plugin.Language, "中文"); shotStep++; } break;
                case 16: if (t > 44f) { Shot("f07_menu_zh"); shotStep++; } break;
                case 17: if (t > 45f) { TourSet(Plugin.Language, "Français"); gui.SetMenu(false); gui.ToggleWeekPlan(); shotStep++; } break;
                case 18: if (t > 48f) { Shot("f08_weekplan_fr"); shotStep++; } break;
                case 19: if (t > 49f) { gui.ToggleWeekPlan(); TourSet(Plugin.Language, "Español"); TourSet(Plugin.MenuScale, 150); gui.SetMenu(true); shotStep++; } break;
                case 20: if (t > 52f) { Shot("f09_menu_scale150_es"); shotStep++; } break;
                case 21: if (t > 53f) { gui.SetMenu(false); TourRestore(); Pins.List.Clear(); shotStep++; } break;
            }
        }

        private static PinButton FindPin(string keyPrefix, bool header = false)
        {
            foreach (PinButton pb in UnityEngine.Object.FindObjectsByType<PinButton>(FindObjectsSortMode.None))
                if (pb.isActiveAndEnabled && pb.Key != null && pb.Key.StartsWith(keyPrefix) && pb.Make != null && (!header || pb.transform.parent.GetComponentInParent<UIBaseCraftSelectionWindow>() != null)) return pb;
            return null;
        }

        private static void OpenSomeQuest()
        {
            foreach (QuestData q in MainGame.Instance.GameSave.questSystemData.questCollection.quests)
            {
                if (q.status != QuestStatus.InProgress || q.isHidden) continue;
                Plugin.Log.LogInfo("[BENCH] quest " + q.id);
                LazyUI.GetWindow<UIQuestInfoWindow>().Open(new UIQuestInfoWindowData(q));
                return;
            }
        }

        private static void OpenFirstRecipeDetail()
        {
            foreach (UICraftPreviewItemCell c in UnityEngine.Object.FindObjectsByType<UICraftPreviewItemCell>(FindObjectsSortMode.None))
            {
                var d = HarmonyLib.Traverse.Create(c).Field("data").GetValue<UICraftPreviewItemCellData>();
                if (d?.CraftDef == null || d.IsTab || d.IsUnknown) continue;
                HarmonyLib.AccessTools.Method(typeof(UICraftPreviewItemCell), "OpenCraftSetupWindow").Invoke(c, null);
                Plugin.Log.LogInfo("[BENCH] detail " + d.CraftDef.id);
                return;
            }
        }

        private static void CloseCraftWindows()
        {
            foreach (UIBaseCraftSelectionWindow w in UnityEngine.Object.FindObjectsByType<UIBaseCraftSelectionWindow>(FindObjectsSortMode.None)) if (w.IsShown) w.Close();
            LazyUI.GetWindow<UICraftWindow>().Close();
        }

        private static void LogNewestShot()
        {
            try
            {
                var files = new System.IO.DirectoryInfo(HiResShot.Folder).GetFiles("*.png");
                Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
                if (files.Length == 0) { Plugin.Log.LogWarning("[BENCH] no hi-res shot"); return; }
                byte[] h = new byte[24];
                using (var fs = System.IO.File.OpenRead(files[0].FullName)) fs.Read(h, 0, 24);
                int w = (h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19], hh = (h[20] << 24) | (h[21] << 16) | (h[22] << 8) | h[23];
                Plugin.Log.LogInfo("[BENCH] hires " + files[0].Name + " " + w + "x" + hh + " screen " + Screen.width + "x" + Screen.height);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[BENCH] hires check: " + e.Message); }
        }
        private string origLang;

        // Spielsprache nur fuer die Store-Bilder umstellen (nicht gespeichert)
        private static void SetGameLanguage(string lang)
        {
            GameSettings gs = GameSettings.Instance;
            gs.language = lang;
            gs.ApplyLanguageSettings(applySave: false);
            try { GUIElements.Instance?.UpdateLocalizedLabels(); } catch { }
        }

        // Store-Screenshots, ein Bild je Feature. Zeiten in Sekunden nach dem Laden.
        private readonly Dictionary<ConfigEntryBase, object> tourSaved = new Dictionary<ConfigEntryBase, object>();
        private void TourSet(ConfigEntryBase e, object v) { if (!tourSaved.ContainsKey(e)) tourSaved[e] = e.BoxedValue; e.BoxedValue = v; }
        private void TourRestore() { foreach (var kv in tourSaved) kv.Key.BoxedValue = kv.Value; tourSaved.Clear(); }

        private void SteamTour(float t)
        {
            TweaksGui gui = Plugin.Instance.Gui;
            switch (shotStep)
            {
                case 0: if (t > 10f) { Shot("s02_gameplay"); shotStep++; } break;
                case 1: if (t > 11f)
                    {
                        TourSet(Plugin.ShowOverlay, true); TourSet(Plugin.OvCorner, "TopRight"); TourSet(Plugin.OvLayout, "Row");
                        foreach (var e in new ConfigEntryBase[] { Plugin.OvFps, Plugin.OvLows, Plugin.OvCpu, Plugin.OvRam, Plugin.OvVram, Plugin.OvWeekday, Plugin.OvGameTime }) TourSet(e, true);
                        TourSet(Plugin.OvFrameTime, false); TourSet(Plugin.OvGpu, false); TourSet(Plugin.OvResolution, false); TourSet(Plugin.OvClock, false);
                        shotStep++;
                    } break;
                case 2: if (t > 14f) { Shot("s03_overlay"); shotStep++; } break;
                case 3: if (t > 15f) { TourSet(Plugin.ShowOverlay, false); gui.SetMenu(true); shotStep++; } break;
                case 4: if (t > 18f) { Shot("s04_modmenu"); shotStep++; } break;
                case 5: if (t > 19f) { gui.ScrollToHeader(Labels.T("Spielstand-Backups", "Save backups")); shotStep++; } break;
                case 6: if (t > 21f) { Shot("s05_backups"); shotStep++; } break;
                case 7: if (t > 22f) { gui.SetMenu(false); shotStep++; } break;
                case 8: if (t > 24f) { Try(() => LazyUI.GetWindow<UIGamePauseWindow>().Open(null)); shotStep++; } break;
                case 9: if (t > 27f) { Shot("s06_pausemenu"); shotStep++; } break;
                case 10: if (t > 28f) { Try(() => LazyUI.GetWindow<UIGamePauseWindow>().Close()); shotStep++; } break;
                case 11: if (t > 30f) { gui.ToggleWeekPlan(); shotStep++; } break;
                case 12: if (t > 33f) { Shot("s07_weekplan"); shotStep++; } break;
                case 13: if (t > 34f) { gui.ToggleWeekPlan(); WeekPlan.OnNewDay(WeekPlan.TodayNumber); shotStep++; } break;
                case 14: if (t > 36f) { Shot("s08_reminder"); shotStep++; } break;
                case 15: if (t > 42f) { Try(OpenNearestWorkbench); shotStep++; } break;
                case 16: if (t > 45f) { Try(PinFirstRecipes); Try(PinOtherWorkbenches); shotStep++; } break;
                case 17: if (t > 47f) { Shot("s09_pins_workbench"); shotStep++; } break;
                case 18: if (t > 48f) { Try(() => LazyUI.GetWindow<UICraftWindow>().Close()); shotStep++; } break;
                case 19: if (t > 51f) { Shot("s10_pins"); shotStep++; } break;
                case 20: if (t > 52f) { Pins.List.Clear(); Try(OpenSomeQuest); shotStep++; } break;
                case 50: break;
                case 21: if (t > 54f) { Try(() => { PinButton b = FindPin("quest:"); if (b != null) Pins.Toggle(b.Make()); }); shotStep = 40; } break;
                case 40: if (t > 56f) { Shot("s16_quest_pin"); shotStep++; } break;
                case 41: if (t > 57f) { Try(() => LazyUI.GetWindow<UIQuestInfoWindow>().Close()); Pins.List.Clear(); HudToggle.Toggle(); shotStep++; } break;
                case 42: if (t > 59f) { Shot("s11_nohud"); shotStep = 22; } break;
                case 22: if (t > 60f) { HudToggle.Show(); gui.ShowNews(true); shotStep++; } break;
                case 23: if (t > 63f) { Shot("s12_whatsnew"); shotStep++; } break;
                case 24: if (t > 64f) { Plugin.LastSeenVersion.Value = "1.2.0"; Try(() => AccessTools_CloseNews(gui)); GraphicsBench.Start(); shotStep++; } break;
                case 25: if (t > 74f) { Shot("s13_benchmark_running"); shotStep++; } break;
                case 26: if (!GraphicsBench.Running && t > 84f) { shotStep++; } break;
                case 27: if (t > 145f) { Shot("s14_benchmark_result"); shotStep++; } break;
                case 28: if (t > 147f) { gui.SetMenu(false); TourRestore(); Plugin.LastSeenVersion.Value = "1.2.0"; if (origLang != null) Try(() => SetGameLanguage(origLang)); shotStep++; } break;
            }
        }

        private static void AccessTools_CloseNews(TweaksGui gui) => HarmonyLib.AccessTools.Method(typeof(TweaksGui), "CloseNews").Invoke(gui, null);

        // Rezepte anderer Werkbaenke in der Naehe anpinnen, bevorzugt solche mit fehlenden Zutaten (rot/gruen im Bild)
        private static void PinOtherWorkbenches()
        {
            Vector3 pos = MainGame.PlayerController.transform.position;
            var wgos = new List<Wgo>(UnityEngine.Object.FindObjectsByType<Wgo>(FindObjectsSortMode.None));
            wgos.Sort((a, b) => Vector3.Distance(pos, a.transform.position).CompareTo(Vector3.Distance(pos, b.transform.position)));
            var seen = new HashSet<string>();
            foreach (Wgo w in wgos)
            {
                CraftComponent cc = w.Data?.CraftComponent;
                if (cc == null || cc.CraftsIn == null || w.Data.Definition == null || !seen.Add(w.Data.Definition.id)) continue;
                if (w.Data.Definition.id == "circular_saw") continue;
                foreach (CraftDefBase cb in cc.CraftsIn)
                {
                    CraftDef def = cb as CraftDef;
                    if (def == null || def.isAuto || def.needItems == null || def.needItems.Count < 2) continue;
                    if (def.isNeedsUnlock && !MainGame.Instance.GameSave.knowledgeSystem.unlockedCrafts.Contains(def.id)) continue;
                    Pins.Pin p = Pins.FromCraftDef(def, w.Data);
                    Pins.Toggle(p);
                    if (p.Ready) { Pins.Unpin(p); continue; } // fuers Bild: Rezepte, fuer die noch etwas fehlt
                    Plugin.Log.LogInfo("[BENCH] pinned other " + w.Data.Definition.id + "/" + def.id);
                    break;
                }
                if (Pins.List.Count >= 3) break;
            }
        }

        private static void Try(Action a)
        {
            try { a(); } catch (Exception e) { Plugin.Log.LogWarning("[BENCH] tour: " + e); }
        }

        private static void ClickNamed(string name)
        {
            foreach (LazyButton b in Resources.FindObjectsOfTypeAll<LazyButton>())
                if (b != null && b.name == name && b.gameObject.activeInHierarchy) { Plugin.Log.LogInfo("[BENCH] click " + name); b.onClick.Invoke(); return; }
            Plugin.Log.LogWarning("[BENCH] button not found: " + name);
        }

        private static void OpenNearestWorkbench()
        {
            Vector3 pos = MainGame.PlayerController.transform.position;
            Wgo best = null; float bestD = float.MaxValue;
            foreach (Wgo w in UnityEngine.Object.FindObjectsByType<Wgo>(FindObjectsSortMode.None))
            {
                CraftComponent cc = w.Data?.CraftComponent;
                if (cc == null || cc.CraftsIn == null || cc.CraftsIn.Count < 2) continue;
                if (w.Data.CraftableType != CraftableType.Regular) continue;
                float d = Vector3.Distance(pos, w.transform.position);
                if (d < bestD) { bestD = d; best = w; }
            }
            if (best == null) { Plugin.Log.LogWarning("[BENCH] no workbench"); return; }
            Plugin.Log.LogInfo("[BENCH] workbench " + best.Data.Definition?.id + " d=" + bestD.ToString("0.0"));
            LazyUI.GetWindow<UICraftWindow>().Open(new UIBaseCraftWindowData(best, ce => { }, ce => { }), null);
        }

        private static void PinFirstRecipes()
        {
            int n = 0;
            foreach (PinButton pb in UnityEngine.Object.FindObjectsByType<PinButton>(FindObjectsSortMode.None))
            {
                if (!pb.gameObject.activeInHierarchy || pb.Make == null) continue;
                Pins.Toggle(pb.Make());
                if (++n >= (Steam ? 1 : 2)) break;
            }
            Plugin.Log.LogInfo("[BENCH] pinned " + n);
            foreach (Pins.Pin p in Pins.List)
                Plugin.Log.LogInfo("[BENCH] pin " + p.Key + " '" + p.Title + "' " + string.Join(", ", p.Needs.ConvertAll(x => x.Name + " " + x.Have + "/" + x.Count).ToArray()));
        }

        private static void Shot(string name)
        {
            string path = Path.Combine(Paths.BepInExRootPath, "shot_" + name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Plugin.Log.LogInfo("[BENCH] Screenshot: " + path);
        }

        private void Report(string variant)
        {
            LoopProfiler.Active = false;
            SystemProfiler.Active = false;
            FrameResult r = stats.Compute();
            string label = Plugin.BenchLabel.Value + "/" + variant;
            Plugin.Log.LogInfo("[BENCH] PROFILE label=" + label + " gc0=" + (GC.CollectionCount(0) - gcStart) + " " + LoopProfiler.Report(r.Frames, 12));
            Plugin.Log.LogInfo("[BENCH] SYSTEMS label=" + label + " " + SystemProfiler.Report(r.Frames));
            Plugin.Log.LogInfo("[BENCH] RESULT label=" + label + " " + r.ToLogString());
        }

        private void Fail(string reason)
        {
            Plugin.Log.LogError("[BENCH] FAIL label=" + Plugin.BenchLabel.Value + " " + reason);
            RestoreBaseline();
            phase = Phase.Done;
            Application.Quit();
        }

        private void SetPhase(Phase p, float now)
        {
            phase = p;
            phaseStart = now;
        }
    }
}

#endif
