using System;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "mats.gk2.tweaks";
        public const string PluginName = "GK2 Tweaks";
        public const string PluginVersion = "1.4.0";
        internal const string Keep = "Default";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        // [Graphics] – einzelne Schalter, die das Spiel intern hat, am PC aber nur als feste Stufen anbietet
        internal static ConfigEntry<string> RenderMode, Shadows, Hbao, PointLights, BackLight, Water, Clouds;
        // [FrameRate]
        internal static ConfigEntry<string> Pacing;
        internal static ConfigEntry<int> TargetFps;
        // [Performance]
        internal static ConfigEntry<int> PhysicsHz, Zoom, AutoSaveMinutes;
        internal static ConfigEntry<bool> PauseInBackground, MenuExtend, MenuModdedLabel, SkipIntro, GameMenuButton;
        internal static ConfigEntry<string> GameLog;
        // [Interface]
        internal static ConfigEntry<KeyboardShortcut> MenuKey, OverlayKey, SaveKey, WeekPlanKey, HudKey;
        internal static ConfigEntry<bool> WeekPlanNotify;
        internal static ConfigEntry<int> BackupCount, BackupMinutes;
        internal static ConfigEntry<bool> ShowOverlay, CheckUpdates;
        internal static ConfigEntry<int> StatsLogSeconds;
        internal static ConfigEntry<string> Language;
        // [Overlay] – FPS-Anzeige
        internal static ConfigEntry<string> OvCorner, OvLayout, PinsCorner, PinsSize;
        internal static ConfigEntry<bool> PinsEnabled;
        internal static ConfigEntry<bool> OvFps, OvLows, OvFrameTime, OvCpu, OvGpu, OvRam, OvVram, OvResolution, OvClock, OvWeekday, OvGameTime;
#if DEV
        // [Benchmark] – nur fuer Messlaeufe (Dev-Build)
        internal static ConfigEntry<bool> BenchEnabled, BenchMenuShot;
        internal static ConfigEntry<string> BenchLabel, BenchVariants;
        internal static ConfigEntry<int> BenchWarmup, BenchMeasure;
        internal static bool BenchOn => BenchEnabled.Value;
#else
        internal static bool BenchOn => false;
#endif

        internal TweaksGui Gui;
        private float defaultFixedDeltaTime;
        private readonly FrameStats logStats = new FrameStats();
        private float logFlushAt;
#if DEV
        private Benchmark bench;
#endif

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            defaultFixedDeltaTime = Time.fixedDeltaTime;
            BindConfig();

            var harmony = new Harmony(Guid);
            var patches = new System.Collections.Generic.List<Type> { typeof(TierPatch), typeof(ScreenSettingsPatch), typeof(SaveBlockPatch), typeof(ZoomPatch), typeof(ModdedLabelPatch), typeof(BackupPatch), typeof(MainMenuModsButtonPatch), typeof(PauseModsButtonPatch), typeof(CraftPinPatch), typeof(CraftCellPinPatch), typeof(BuildPinPatch), typeof(TownPinPatch) };
#if DEV
            if (BenchEnabled.Value) patches.Add(typeof(SystemProfiler));
#endif
            foreach (Type t in patches)
            {
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception e) { Log.LogError("Patch " + t.Name + " fehlgeschlagen: " + e.Message); }
            }

            ApplyPhysics();
            ApplyLogFilter();
            Application.runInBackground = !PauseInBackground.Value;
            Config.SettingChanged += OnSettingChanged;

            Gui = gameObject.AddComponent<TweaksGui>();
            StartCoroutine(UpdateCheck.Run());
            WeekPlan.Init();
#if DEV
            if (BenchEnabled.Value) bench = new Benchmark();
#endif
            Log.LogInfo(PluginName + " " + PluginVersion + " loaded" + (WineFix.IsWine ? " (Wine/CrossOver)" : ""));
        }

        private void BindConfig()
        {
            RenderMode = Config.Bind("Graphics", "RenderMode", Keep, new ConfigDescription(
                "Native = world at full resolution. Lightweight = world at half resolution (chunkier pixels), much less GPU load. Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "Native", "Lightweight")));
            Shadows = Config.Bind("Graphics", "Shadows", Keep, new ConfigDescription(
                "Shadows. NGSS = soft PC shadows (expensive). Unity_* = simpler shadows as on PS5/Xbox. Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "Off", "NGSS_High", "NGSS_Medium", "NGSS_Low", "Unity_Desktop", "Unity_Balanced", "Unity_Performance", "Unity_Low", "Unity_Minimal", "Unity_Console")));
            Hbao = Config.Bind("Graphics", "AmbientOcclusion", Keep, new ConfigDescription(
                "Ambient occlusion (HBAO). Off is otherwise only used on Switch. Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "Off", "Lowest", "Low", "Medium", "High", "Highest")));
            PointLights = Config.Bind("Graphics", "PointLights", Keep, new ConfigDescription(
                "Point lights real (Realtime) or simulated (Faked, cheaper). Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "Realtime", "Faked")));
            BackLight = Config.Bind("Graphics", "BackLight", Keep, new ConfigDescription(
                "Back light effect. Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "On", "Off")));
            Water = Config.Bind("Graphics", "Water", Keep, new ConfigDescription(
                "Water quality. Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "High", "Light")));
            Clouds = Config.Bind("Graphics", "Clouds", Keep, new ConfigDescription(
                "Clouds normal or simplified. May apply only after changing area. Default = as the graphics tier.",
                new AcceptableValueList<string>(Keep, "Normal", "Replaced")));

            Pacing = Config.Bind("FrameRate", "Mode", "Game", new ConfigDescription(
                "Game = setting from the game menu. Limit = software cap at TargetFps. VSync = synced to the refresh rate (60 FPS at 120 Hz = every 2nd refresh).",
                new AcceptableValueList<string>("Game", "Limit", "VSync")));
            TargetFps = Config.Bind("FrameRate", "TargetFps", 60, new ConfigDescription(
                "Target FPS for Limit and VSync. 0 = unlimited / full refresh rate.",
                new AcceptableValueList<int>(0, 30, 40, 45, 50, 60, 72, 90, 120, 144)));

            PhysicsHz = Config.Bind("Performance", "PhysicsHz", 0, new ConfigDescription(
                "Physics rate. 0 = game default (50 Hz). 30 = like the Switch version, saves CPU.",
                new AcceptableValueList<int>(0, 60, 50, 40, 30)));
            GameLog = Config.Bind("Performance", "GameLog", "All", new ConfigDescription(
                "What the game writes to Player.log. Warning or Error suppresses log spam.",
                new AcceptableValueList<string>("All", "Warning", "Error")));

            Zoom = Config.Bind("Comfort", "Zoom", 100, new ConfigDescription(
                "Camera zoom in percent. Below 100 = see more, above 100 = closer.",
                new AcceptableValueList<int>(60, 70, 75, 80, 90, 100, 110, 125, 150)));
            PauseInBackground = Config.Bind("Comfort", "PauseInBackground", true, "Pause the game while its window is in the background (saves battery and heat).");
            MenuExtend = Config.Bind("Comfort", "MainMenuExtend", true, "Fill the sides of the main menu on ultrawide screens with a blurred copy of the menu image.");
            SkipIntro = Config.Bind("Comfort", "SkipIntro", false, "Skip the logos and intro videos when the game starts.");
            MenuModdedLabel = Config.Bind("Comfort", "MainMenuModdedLabel", true, "Show a 'modded' note next to the version number in the main menu.");
            GameMenuButton = Config.Bind("Comfort", "GameMenuButton", true, "Show a 'Mods' button in the main menu and the pause menu (opens this mod menu).");
            AutoSaveMinutes = Config.Bind("Comfort", "AutoSaveMinutes", 0, new ConfigDescription(
                "Extra autosave every N minutes (0 = off). Only saves while you are in free control.",
                new AcceptableValueList<int>(0, 5, 10, 15, 20, 30)));

            MenuKey = Config.Bind("Interface", "MenuKey", new KeyboardShortcut(KeyCode.F9), "Key for the mod menu.");
            OverlayKey = Config.Bind("Interface", "OverlayKey", new KeyboardShortcut(KeyCode.F10), "Key for the FPS display.");
#if !NEXUS
            CheckUpdates = Config.Bind("Interface", "CheckForUpdates", true, "Check GitHub once per game start for a new version of GK2 Vanilla+ (only reads the version number, nothing is sent).");
#endif
            WeekPlanKey = Config.Bind("Interface", "WeekPlanKey", new KeyboardShortcut(KeyCode.F6), "Key for the week plan (what is possible on which weekday).");
            HudKey = Config.Bind("Interface", "HideHudKey", new KeyboardShortcut(KeyCode.F7), "Key to hide/show the game's HUD, e.g. for screenshots. Esc shows it again.");
            WeekPlanNotify = Config.Bind("Comfort", "DailyReminder", true, "Show a notification each morning with what is possible today (only features you have already unlocked).");
            BackupCount = Config.Bind("Backups", "KeepBackups", 5, new ConfigDescription(
                "Before the game overwrites a save, the previous save is backed up (BepInEx/GK2VanillaPlus/Backups). Number of backups kept per save slot, 0 = off.",
                new AcceptableValueList<int>(0, 3, 5, 10, 20)));
            BackupMinutes = Config.Bind("Backups", "MinMinutesBetween", 10, new ConfigDescription(
                "Minimum minutes between two backups of the same slot (avoids a backup on every autosave).",
                new AcceptableValueList<int>(0, 5, 10, 15, 30, 60)));
            SaveKey = Config.Bind("Interface", "SaveKey", KeyboardShortcut.Empty, "Key for saving the game manually (empty = only the button in the mod menu).");
            ShowOverlay = Config.Bind("Interface", "ShowOverlay", false, "Show the FPS display (toggle with F10). Position and contents: section [Overlay].");
            Language = Config.Bind("Interface", "Language", "Auto", new ConfigDescription(
                "Language of the mod menu. Auto = game language (German if the game is set to German, otherwise English).",
                new AcceptableValueList<string>("Auto", "Deutsch", "English")));
            OvCorner = Config.Bind("Overlay", "Corner", "BottomLeft", new ConfigDescription("Screen corner of the FPS display.",
                new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
            OvLayout = Config.Bind("Overlay", "Layout", "Row", new ConfigDescription("Row = all values in one line. Column = one value per line.",
                new AcceptableValueList<string>("Row", "Column")));
            OvFps = Config.Bind("Overlay", "Fps", true, "Frames per second (average over 0.5 s).");
            OvLows = Config.Bind("Overlay", "Lows", true, "1% low FPS: how smooth it feels. Close to the FPS value = no stutter.");
            OvFrameTime = Config.Bind("Overlay", "FrameTime", false, "Frame time in ms (average and slowest frame).");
            OvCpu = Config.Bind("Overlay", "Cpu", false, "CPU load of the game (100 % = all cores busy).");
            OvGpu = Config.Bind("Overlay", "Gpu", false, "GPU load (3D engine, like Task Manager). Windows only.");
            OvRam = Config.Bind("Overlay", "Ram", false, "RAM used by the game / installed RAM.");
            OvVram = Config.Bind("Overlay", "Vram", false, "Video memory used by the game's textures and buffers / GPU memory.");
            OvResolution = Config.Bind("Overlay", "Resolution", false, "Current render resolution.");
            OvClock = Config.Bind("Overlay", "Clock", false, "Current time.");
            OvWeekday = Config.Bind("Overlay", "Weekday", false, "In-game weekday (Pride, Sloth, ...).");
            OvGameTime = Config.Bind("Overlay", "GameTime", false, "In-game time of day.");
            PinsEnabled = Config.Bind("Pins", "Enabled", true, "Pin recipes, blueprints and town buildings (pin icon in their top right corner). Pinned items show have/need counts from your inventory.");
            PinsCorner = Config.Bind("Pins", "Corner", "TopRight", new ConfigDescription("Screen corner of the pinned list.",
                new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
            PinsSize = Config.Bind("Pins", "Size", "Medium", new ConfigDescription("Text and icon size of the pinned list.",
                new AcceptableValueList<string>("Small", "Medium", "Large", "ExtraLarge")));
            StatsLogSeconds = Config.Bind("Interface", "StatsLogSeconds", 0, new ConfigDescription(
                "Write frame statistics to the BepInEx log every N seconds (0 = off).",
                new AcceptableValueList<int>(0, 5, 10, 30, 60)));

#if DEV
            BenchEnabled = Config.Bind("Benchmark", "Enabled", false, "Nur fuer Messlaeufe: laedt den letzten Spielstand (Speichern blockiert), misst und beendet das Spiel.");
            BenchLabel = Config.Bind("Benchmark", "Label", "run", "Name des Messlaufs.");
            BenchVariants = Config.Bind("Benchmark", "Variants", "base", "Semikolon-getrennte Varianten, je Variante komma-getrennte Zuweisungen, z. B. base;hbao=Off;physics=30,log=Error;tier=High;res=1920x1080");
            BenchWarmup = Config.Bind("Benchmark", "WarmupSeconds", 20, "Wartezeit nach dem Laden.");
            BenchMeasure = Config.Bind("Benchmark", "MeasureSeconds", 45, "Messdauer in Sekunden.");
            BenchMenuShot = Config.Bind("Benchmark", "MenuScreenshot", false, "Menue kurz oeffnen und Screenshot speichern (Test).");
#endif
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            switch (e.ChangedSetting.Definition.Section)
            {
                case "Graphics": ReapplyGraphics(); break;
                case "FrameRate": ReapplyPacing(); break;
                case "Performance": ApplyPhysics(); ApplyLogFilter(); break;
                case "Pins": PinButton.RefreshAll(); break;
                case "Comfort": ZoomPatch.Reapply(); Application.runInBackground = !PauseInBackground.Value; ModsButton.ApplyVisibility(); break;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (MenuKey.Value.IsDown()) Gui.ToggleMenu();
            if (OverlayKey.Value.IsDown()) ShowOverlay.Value = !ShowOverlay.Value;
            if (SaveKey.Value.MainKey != KeyCode.None && SaveKey.Value.IsDown()) ManualSave.Save(Gui.MenuOpen);
            if (WeekPlanKey.Value.MainKey != KeyCode.None && WeekPlanKey.Value.IsDown()) Gui.ToggleWeekPlan();
            if (HudKey.Value.MainKey != KeyCode.None && HudKey.Value.IsDown()) HudToggle.Toggle();
            HudToggle.Tick();
            GraphicsBench.Tick(dt);
            Pins.Tick();
            Gui.Tick(dt);

            if (StatsLogSeconds.Value > 0)
            {
                logStats.Add(dt);
                float now = Time.realtimeSinceStartup;
                if (now >= logFlushAt)
                {
                    if (logStats.Count > 0) Log.LogInfo("[STATS] " + logStats.Compute().ToLogString());
                    logStats.Reset();
                    logFlushAt = now + StatsLogSeconds.Value;
                }
            }
#if DEV
            bench?.Update(dt);
#endif
            AutoSave.Tick();
            ZoomPatch.Tick();
            MenuSideFill.Tick();
            SkipLogosPatch.Tick();
#if DEV
            UiDump.Tick();
#endif
        }

        internal static void ReapplyGraphics()
        {
            try
            {
                PlatformFeatures.ReapplyAll();
                Log.LogInfo("Grafik angewendet: " + DescribeFeatures());
            }
            catch (Exception ex) { Log.LogWarning("Grafik konnte nicht angewendet werden: " + ex.Message); }
        }

        internal static void ReapplyPacing()
        {
            if (Pacing.Value == "Game")
            {
                try { GameSettings.Instance?.ApplyScreenSettings(); } catch (Exception ex) { Log.LogWarning(ex.Message); }
                return;
            }
            ApplyPacing();
        }

        internal static void ApplyPacing()
        {
            string mode = Pacing.Value;
            if (mode == "Game") return;
            int fps = TargetFps.Value;
            if (mode == "Limit")
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = fps > 0 ? fps : -1;
            }
            else
            {
                int hz = MonitorHz();
                int interval = (fps > 0 && hz > 0) ? Mathf.Clamp(Mathf.RoundToInt((float)hz / fps), 1, 4) : 1;
                QualitySettings.vSyncCount = interval;
                Application.targetFrameRate = -1;
            }
            Log.LogInfo($"Bildrate: {mode}, Ziel {fps}, vSyncCount {QualitySettings.vSyncCount}, targetFrameRate {Application.targetFrameRate}, Monitor {MonitorHz()} Hz");
        }

        internal static void ApplyPhysics()
        {
            int hz = PhysicsHz.Value;
            Time.fixedDeltaTime = hz > 0 ? 1f / hz : Instance.defaultFixedDeltaTime;
        }

        internal static void ApplyLogFilter()
        {
            switch (GameLog.Value)
            {
                case "Error": Debug.unityLogger.filterLogType = LogType.Error; break;
                case "Warning": Debug.unityLogger.filterLogType = LogType.Warning; break;
                default: Debug.unityLogger.filterLogType = LogType.Log; break;
            }
        }

        internal static int MonitorHz()
        {
            double v = Screen.currentResolution.refreshRateRatio.value;
            return v > 0 ? Mathf.RoundToInt((float)v) : 0;
        }

        internal static string DescribeFeatures()
        {
            try
            {
                PlatformFeatureEntry e = PlatformFeatures.Current;
                if (e == null) return "-";
                string shadow = e.shadowMode == PlatformShadowMode.NGSS ? "NGSS " + e.ngssQuality
                    : e.shadowMode == PlatformShadowMode.Unity ? "Unity " + e.unityShadowPreset : Labels.T("aus", "off");
                return $"Render {e.renderMode} | {Labels.T("Schatten", "Shadows")} {shadow} | HBAO {e.hbaoQuality} | {Labels.T("Licht", "Lights")} {e.pointLightMode} | {Labels.T("Gegenlicht", "Back light")} {(e.backLightEnabled ? Labels.T("an", "on") : Labels.T("aus", "off"))} | {Labels.T("Wasser", "Water")} {e.waterTier} | {Labels.T("Wolken", "Clouds")} {e.cloudAppearance}";
            }
            catch (Exception ex) { return "? (" + ex.Message + ")"; }
        }

        internal static string DescribeState()
        {
            string tier = GameSettings.Instance != null ? GameSettings.Instance.graphicsTier.ToString() : "?";
            return string.Format(CultureInfo.InvariantCulture,
                "tier={0} pacing={1}/{2} vSyncCount={3} targetFrameRate={4} monitorHz={5} physicsHz={6} gameLog={7} screen={8}x{9} api='{10}' gpu='{11}' features=[{12}]",
                tier, Pacing.Value, TargetFps.Value, QualitySettings.vSyncCount, Application.targetFrameRate, MonitorHz(),
                PhysicsHz.Value, GameLog.Value, Screen.width, Screen.height, SystemInfo.graphicsDeviceVersion, SystemInfo.graphicsDeviceName,
                DescribeFeatures());
        }
    }
}
