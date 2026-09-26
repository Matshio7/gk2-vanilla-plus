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
                    if (menuSince < 0f) { menuSince = now; return; }
                    if (Plugin.BenchMenuShot.Value && !mainShot && now - menuSince >= 4f) { Shot("mainmenu"); mainShot = true; return; }
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
                    if (Plugin.BenchMenuShot.Value) MenuShot(inPhase);
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
