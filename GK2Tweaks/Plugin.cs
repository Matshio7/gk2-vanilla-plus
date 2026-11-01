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
    [BepInDependency(ModsButton.FrameworkGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "mats.gk2.tweaks";
        public const string PluginName = "GK2 Tweaks";
        public const string PluginVersion = "1.7.6";
        // Anzeige (Hauptmenue, Was ist neu, Log). Hotfix-Buchstaben (z. B. "b") nur bei schnellen, nicht voll getesteten Hotfixes. PluginVersion bleibt
        // eine reine Zahl - BepInEx, Update-Check und Installer lesen sie als Version.
        // Vorab-Versionen (Beta): BetaNumber > 0 und BetaTag " Beta N" setzen; Stabil: 0 und "". GitHub-Tag der Beta: v<Version>-beta.<N>
        internal const int BetaNumber = 0;
        internal const string BetaTag = "";
        internal const string DisplayVersion = PluginVersion + BetaTag;
        internal const string Keep = "Default";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        // [Graphics] – einzelne Schalter, die das Spiel intern hat, am PC aber nur als feste Stufen anbietet
        internal static ConfigEntry<string> RenderMode, Shadows, Hbao, PointLights, BackLight, Water, Clouds;
        // [FrameRate]
        internal static ConfigEntry<string> Pacing;
        internal static ConfigEntry<int> TargetFps;
        internal static ConfigEntry<string> NoTearing;
        // [Performance]
        internal static ConfigEntry<int> PhysicsHz, Zoom, AutoSaveMinutes, InteriorZoom, MenuScale, ShotScale;
        internal static ConfigEntry<bool> MouseWheelZoom, SmoothZoom, HighContrast, ShotHideHud, OledBlack, WideRain, HudCenter;
        internal static ConfigEntry<string> ZoomPresets;
        internal static ConfigEntry<KeyboardShortcut> ZoomPresetKey, ShotKey;
        internal static ConfigEntry<bool> PauseInBackground, MenuExtend, MenuModdedLabel, SkipIntro, GameMenuButton;
        internal static ConfigEntry<string> GameLog;
        // [Interface]
        internal static ConfigEntry<KeyboardShortcut> MenuKey, OverlayKey, SaveKey, WeekPlanKey, HudKey;
        internal static ConfigEntry<bool> WeekPlanNotify, InstantRemove, HudClock, HudClock12h, EscLeave, FasterTransitions, LessMemoryCleanup;
        internal static ConfigEntry<bool> FullRefund, MoveObjects, TradeLikes, ZombieRename, Celebrated, CraftMaxButton, Respec;
        internal static ConfigEntry<string> HudClockMode;
        internal static ConfigEntry<int> BackupCount, BackupMinutes, RainAmount, MenuBgBlur, MenuBgDim, MenuBgFog;
        internal static ConfigEntry<string> MenuBg, MenuBgStyle, MenuBgFogTone;
        internal static ConfigEntry<bool> ShowOverlay, CheckUpdates, RepairWorkbenches, SafeStartGuard;
        internal static ConfigEntry<int> StatsLogSeconds;
        internal static ConfigEntry<int> MoveSpeed;
        internal static ConfigEntry<string> Language, LastSeenVersion, LastGameVersion;
#if !NEXUS
        internal static ConfigEntry<int> RatePromptSessions, RatePromptNextAt;
        internal static ConfigEntry<bool> RatePromptDone;
        internal const string WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3808053878";
#endif
        internal const string KofiUrl = "https://ko-fi.com/mcfly7";
#if MINIMAP
        // [Minimap]
        internal static ConfigEntry<bool> MinimapEnabled;
        internal static ConfigEntry<string> MinimapCorner;
        internal static ConfigEntry<int> MinimapSize, MinimapZoom;
        internal static ConfigEntry<KeyboardShortcut> MinimapKey;
#endif
        // [Overlay] – FPS-Anzeige
        internal static ConfigEntry<string> UpdateChannel, OvCorner, OvLayout, PinsCorner, PinsSize, PinsClick, OvOrder, OvSeparator;
        internal static ConfigEntry<bool> PinsEnabled, PinsNotify, PinsAutoUnpin, PinsVariants, PinsTree, PinsFuel;
        internal static ConfigEntry<string> PinsPadButton;
        internal static ConfigEntry<KeyboardShortcut> PinsKey;
        internal static ConfigEntry<string> PinsChests;
        internal static ConfigEntry<bool> OvGpuTemp, OvFps, OvLows, OvFrameTime, OvCpu, OvGpu, OvRam, OvVram, OvResolution, OvClock, OvWeekday, OvGameTime;
#if DEV
        // [Benchmark] – nur fuer Messlaeufe (Dev-Build)
        internal static ConfigEntry<bool> BenchEnabled, BenchMenuShot;
        internal static ConfigEntry<string> BenchLabel, BenchVariants, BenchShotSet, BenchShotRes;
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

        // AppDomain-weiter Schluessel gegen doppeltes Laden: gilt auch, wenn dieselbe DLL aus zwei Ordnern
        // (z. B. Workshop-Kopie) oder von einem zweiten Loader geladen wird - statische Felder waeren dann pro Kopie getrennt.
        private const string LoadedKey = "mats.gk2.tweaks.loaded";

        private void Awake()
        {
            // Zweite Kopie? Dann bleibt diese komplett inaktiv (vor Instance/Log/BindConfig, damit die erste Kopie unberuehrt bleibt).
            // Wert = "Version|Pfad": ist die unterdrueckte Kopie neuer als die aktive, gibt es einen deutlichen Fehler im Log.
            try
            {
                string here = AssemblyLocation();
                object other = AppDomain.CurrentDomain.GetData(LoadedKey);
                if (other != null)
                {
                    string otherText = other as string ?? other.ToString();
                    Logger.LogWarning("GK2 Tweaks already loaded (" + otherText + "); this copy " + PluginVersion + " stays inactive (" + here + ")");
                    try
                    {
                        int bar = otherText.IndexOf('|');
                        string otherVersion = bar > 0 ? otherText.Substring(0, bar).Trim() : "";
                        if (otherVersion.Length > 0 && new Version(PluginVersion) > new Version(otherVersion))
                            Logger.LogError("GK2 Tweaks: the INACTIVE copy " + PluginVersion + " (" + here + ") is NEWER than the active copy " + otherText +
                                " - please remove the old copy so the new version runs.");
                    }
                    catch { }
                    enabled = false;
                    return;
                }
                AppDomain.CurrentDomain.SetData(LoadedKey, PluginVersion + "|" + here);
            }
            catch (Exception e) { Logger.LogWarning("Duplicate load check failed: " + e.Message); }

            Instance = this;
            Log = Logger;
            defaultFixedDeltaTime = Time.fixedDeltaTime;
            Changelog.FreshInstall = !System.IO.File.Exists(Config.ConfigFilePath);
            // Ohne Einstellungen kann nichts laufen: dann bleibt der Mod komplett inaktiv (Update laeuft nicht),
            // statt jeden Frame an fehlenden Eintraegen zu scheitern
            try { BindConfig(); }
            catch (Exception e)
            {
                Log.LogError("Config could not be loaded, Vanilla+ stays inactive: " + e);
                // Schluessel wieder freigeben: eine zweite Kopie (z. B. von einem anderen Loader) darf es dann versuchen
                try { AppDomain.CurrentDomain.SetData(LoadedKey, null); } catch { }
                enabled = false;
                return;
            }

            // Sicherer Start: kam das Spiel beim letzten Mal nicht ins Hauptmenue, laeuft dieser Start ohne Patches/Funktionen
            try { SafeStart.Begin(); } catch (Exception e) { Log.LogError("Safe start check failed: " + e); }
            if (SafeMode.AllOff) { AwakeVanilla(); return; }

            try { SafeMode.CheckGame(); } catch (Exception e) { Log.LogError("Safe mode check failed: " + e); }
            var harmony = new Harmony(Guid);
            // Die Typen stecken bewusst in Lambdas (wie in SafeMode.CheckGame): ein typeof(X), das beim JIT scheitert,
            // trifft so nur diesen einen Eintrag statt das ganze Awake.
            var patches = new System.Collections.Generic.List<Func<Type>> { () => typeof(TierPatch), () => typeof(ScreenSettingsPatch), () => typeof(SaveBlockPatch), () => typeof(ZoomPatch), () => typeof(ModdedLabelPatch), () => typeof(BackupPatch), () => typeof(MainMenuModsButtonPatch), () => typeof(PauseModsButtonPatch), () => typeof(CraftCellPinPatch), () => typeof(SelectionPinPatch), () => typeof(QuestPinPatch), () => typeof(LongNotes), () => typeof(InstantRemovePatch), () => typeof(TeleportPatch), () => typeof(FadeInPatch), () => typeof(FadeOutPatch), () => typeof(CleanupPatch), () => typeof(MoveInputPatch), () => typeof(MoveTargetPatch), () => typeof(MoveCellsPatch), () => typeof(MoveBuildPatch), () => typeof(MoveDisablePatch), () => typeof(TradePressPatch), () => typeof(TradeCountPatch), () => typeof(ZoneRedrawPatch), () => typeof(MoveRemoveLabelPatch), () => typeof(OrderPinPatch), () => typeof(AlchemyPinPatch), () => typeof(RespecOverPatch), () => typeof(RespecOutPatch), () => typeof(RespecTechPatch), () => typeof(CraftRepairPatch), () => typeof(RainScanPatch), () => typeof(TownBuiltPatch), () => typeof(MoveSpeedPatch) };
#if DEV
            if (BenchEnabled.Value) patches.Add(() => typeof(SystemProfiler));
#endif
            SafeMode.ApplyPatches(harmony, patches);

            try { if (SafeMode.On("WorkshopUpload")) WorkshopUpload.Apply(harmony); } catch (Exception e) { Log.LogWarning("Workshop upload patch: " + e.Message); }
            ApplyPhysics();
            ApplyLogFilter();
            Application.runInBackground = true;   // Pause im Hintergrund macht BackgroundPause (Controller bleibt erkannt)
            Config.SettingChanged += OnSettingChanged;

#if !NEXUS
            RatePromptSessions.Value++;
#endif
            Gui = gameObject.AddComponent<TweaksGui>();
            // Jeder Schritt einzeln abgesichert: ein Fehler (z. B. umbenanntes Spiel-Event nach einem Update) soll nicht den Rest von Awake abbrechen
            try { gameObject.AddComponent<BackgroundPause>(); } catch (Exception e) { Log.LogWarning("BackgroundPause: " + e.Message); }
            try { FrameworkBridgeLoader.TryLoad(); } catch (Exception e) { Log.LogWarning("Framework bridge: " + e.Message); }
            try { Translations.WriteTemplate(); } catch (Exception e) { Log.LogWarning("Translation template: " + e.Message); }
            try { StartCoroutine(UpdateCheck.Run()); } catch (Exception e) { Log.LogWarning("Update check: " + e.Message); }
            try { WeekPlan.Init(); } catch (Exception e) { SafeMode.Disable("WeekPlan", "init failed: " + e.Message, false); }
#if DEV
            if (BenchEnabled.Value) bench = new Benchmark();
            gameObject.AddComponent<DevUpload>();
#endif
            Log.LogInfo(PluginName + " " + DisplayVersion + " loaded (game " + Application.version + ")" + (WineFix.IsWine ? " (Wine/CrossOver)" : ""));
        }

        // Sicherer Start: keine Patches, keine Funktions-Komponenten, keine Aenderungen an Spiel-Einstellungen
        // (Physik, Log-Filter, runInBackground) - nur das Mod-Menue (F9, inkl. FPS-Anzeige) und das Log.
        private void AwakeVanilla()
        {
            try { Gui = gameObject.AddComponent<TweaksGui>(); } catch (Exception e) { Log.LogError("Mod menu failed: " + e); }
            Log.LogWarning(PluginName + " " + DisplayVersion + " loaded in SAFE START mode (game " + Application.version + "): no patches, no features, only the mod menu (F9). Restart the game to run Vanilla+ normally.");
        }

        // Ordner dieser DLL (BepInEx/plugins/GK2Tweaks). Das Workshop-Paket legt die Menue-Hintergruende dort ab.
        internal static string PluginDir
        {
            get
            {
                try
                {
                    string loc = typeof(Plugin).Assembly.Location;
                    if (!string.IsNullOrEmpty(loc)) return System.IO.Path.GetDirectoryName(loc);
                }
                catch { }
                return System.IO.Path.Combine(Paths.PluginPath, "GK2Tweaks");
            }
        }

        // Spielversion mindestens min? Application.version hat die Form "1.011" (Ziffern nach dem Punkt als Dezimalstellen).
        // Unbekanntes Format = false (also "aelter") - wer das nutzt, muss damit sicher umgehen.
        internal static bool GameVersionAtLeast(double min)
        {
            try
            {
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(Application.version ?? "", @"^\s*(\d+\.\d+)");
                if (!m.Success) return false;
                double v = double.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                return v >= min - 0.0000001;
            }
            catch { return false; }
        }

        private string AssemblyLocation()
        {
            try
            {
                string loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(loc) ? "?" : loc;
            }
            catch { return "?"; }
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
                "Game = setting from the game menu. Limit = software cap at TargetFps (with No tearing On, or Automatic while VSync is on in the game settings, VSync stays on and the cap follows the refresh rate without dropping below the target, e.g. 72 FPS at 144 Hz). VSync = synced to the refresh rate (60 FPS at 120 Hz = every 2nd refresh).",
                new AcceptableValueList<string>("Game", "Limit", "VSync")));
            TargetFps = Config.Bind("FrameRate", "TargetFps", 60, new ConfigDescription(
                "Target FPS for Limit and VSync. 0 = unlimited / full refresh rate.",
                new AcceptableValueList<int>(0, 30, 40, 45, 50, 60, 72, 90, 120, 144)));

            NoTearing = Config.Bind("FrameRate", "NoTearing", "Auto", new ConfigDescription(
                "Prevents screen tearing: keeps VSync on even when an FPS limit is set (the game turns VSync off then). Auto = on for Steam Deck and Linux, and on Windows when VSync is on in the game settings.",
                new AcceptableValueList<string>("Auto", "On", "Off")));
            PhysicsHz = Config.Bind("Performance", "PhysicsHz", 0, new ConfigDescription(
                "Physics rate. 0 = game default (50 Hz). 30 = like the Switch version, saves CPU.",
                new AcceptableValueList<int>(0, 60, 50, 40, 30)));
            FasterTransitions = Config.Bind("Performance", "FasterTransitions", true, "Doors and map travel: shorter fade to black and pause (about a third). Cutscenes and sleeping are not changed.");
            LessMemoryCleanup = Config.Bind("Performance", "LessMemoryCleanup", false, "Doors and map travel: the game cleans up all memory on every door, which takes most of the waiting time. With this on it only happens every 10 minutes or when memory gets tight (always when loading a save). Uses more RAM - not recommended below 16 GB.");
            GameLog = Config.Bind("Performance", "GameLog", "All", new ConfigDescription(
                "What the game writes to Player.log. Warning or Error suppresses log spam.",
                new AcceptableValueList<string>("All", "Warning", "Error")));

            Zoom = Config.Bind("Comfort", "Zoom", 100, new ConfigDescription(
                "Camera zoom in percent. Below 100 = see more, above 100 = closer.",
                new AcceptableValueList<int>(60, 70, 75, 80, 90, 100, 110, 125, 150)));
            InteriorZoom = Config.Bind("Camera", "InteriorZoom", 0, new ConfigDescription(
                "Separate camera zoom inside buildings (church, morgue, houses ...). 0 = same as outside.",
                new AcceptableValueList<int>(0, 80, 90, 100, 110, 125, 150, 175)));
            ZoomPresets = Config.Bind("Camera", "ZoomPresets", "80,100,125", "Zoom levels in percent the preset key cycles through, comma-separated.");
            ZoomPresetKey = Config.Bind("Camera", "ZoomPresetKey", new KeyboardShortcut(KeyCode.F5), "Key to cycle through the zoom presets.");
            MouseWheelZoom = Config.Bind("Camera", "MouseWheelZoom", true, "Zoom smoothly with the mouse wheel (only while walking around, not over menus). Resets to the camera zoom setting on the next start.");
            SmoothZoom = Config.Bind("Camera", "SmoothZoom", true, "Smooth transition when the zoom changes.");
            PauseInBackground = Config.Bind("Comfort", "PauseInBackground", true, "Pause the game while its window is in the background (saves battery and heat).");
            MenuExtend = Config.Bind("Comfort", "MainMenuExtend", true, "Fill the sides of the main menu on ultrawide screens with a blurred copy of the menu image.");
            SkipIntro = Config.Bind("Comfort", "SkipIntro", false, "Skip the logos and intro videos when the game starts.");
            MenuModdedLabel = Config.Bind("Comfort", "MainMenuModdedLabel", true, "Show a 'modded' note next to the version number in the main menu.");
            GameMenuButton = Config.Bind("Comfort", "GameMenuButton", true, "Show a 'Vanilla+' button in the main menu and the pause menu (opens this mod menu).");
            AutoSaveMinutes = Config.Bind("Comfort", "AutoSaveMinutes", 0, new ConfigDescription(
                "Extra autosave every N minutes (0 = off). Only saves while you are in free control.",
                new AcceptableValueList<int>(0, 5, 10, 15, 20, 30)));

            MenuKey = Config.Bind("Interface", "MenuKey", new KeyboardShortcut(KeyCode.F9), "Key for the mod menu.");
            OverlayKey = Config.Bind("Interface", "OverlayKey", new KeyboardShortcut(KeyCode.F10), "Key for the FPS display.");
#if !NEXUS
            CheckUpdates = Config.Bind("Interface", "CheckForUpdates", true, "Check GitHub once per game start for a new version of GK2 Vanilla+ (only reads the version number, nothing is sent).");
#endif
#if !NEXUS
            UpdateChannel = Config.Bind("Interface", "UpdateChannel", "Stable", new ConfigDescription("Which versions the update check looks for. Stable = tested releases (recommended). Beta = pre-release for testing, may contain bugs.",
                new AcceptableValueList<string>("Stable", "Beta")));
#endif
            RepairWorkbenches = Config.Bind("Fixes", "RepairStuckWorkbenches", false, "Only if a workbench is stuck (no progress, F does nothing, cannot be removed): turn this on, then press F at that workbench or remove it. Vanilla+ checks the workbench, fixes known stuck states (e.g. stuck as running, a waiting craft without ingredients at the front, extensions not linked - nothing is lost) and tells you what it found. Details go to the log. Turn it off again afterwards.");
            WeekPlanKey = Config.Bind("Interface", "WeekPlanKey", new KeyboardShortcut(KeyCode.F6), "Key for the week plan (what is possible on which weekday).");
            HudKey = Config.Bind("Interface", "HideHudKey", new KeyboardShortcut(KeyCode.F7), "Key to hide/show the game's HUD, e.g. for screenshots. Esc shows it again.");
            WeekPlanNotify = Config.Bind("Comfort", "DailyReminder", true, "Show a notification each morning with what is possible today (only features you have already unlocked).");
            HudClock = Config.Bind("Comfort", "HudClock", true, "Show the day and the time of day as a second line in the area name box at the top right, in the game's own look.");
            HudClockMode = Config.Bind("Comfort", "HudClockMode", "DayAndTime", new ConfigDescription("What the HUD line shows: day number and time, weekday and time, or only the time.",
                new AcceptableValueList<string>("DayAndTime", "WeekdayAndTime", "TimeOnly")));
            HudClock12h = Config.Bind("Comfort", "HudClock12h", false, "Show the HUD time as 12-hour clock (5:00 AM) instead of 24-hour (05:00).");
            EscLeave = Config.Bind("Comfort", "EscLeavesConversation", true, "In conversations, Esc or B (Circle) picks \"Leave\" when it is offered and the character has finished talking. An open mod window is closed first.");
            InstantRemove = Config.Bind("Comfort", "InstantRemove", false, "Remove mode (building): placed objects like workbenches, chests or furnaces are removed right away instead of your character walking there first. You get the same materials back. Helps with objects your character cannot reach.");
            FullRefund = Config.Bind("Comfort", "FullRefund", false, "[Not fully vanilla] Remove mode: you get the full building costs back instead of only a part. Contents (inventory, fuel) come back as usual.");
            MoveObjects = Config.Bind("Comfort", "MoveObjects", false, "[Not fully vanilla] Remove mode: press the rotate key on an object to pick it up and place it somewhere else. It stays the same object (contents and crafting queue are kept), nothing is used or refunded. Works for workbenches, conveyors and all other buildings: zombie workers are put on the ground, connected extensions stay where they are until you move them too. Esc or right click cancels.");
            TradeLikes = Config.Bind("Comfort", "TradeLikedAmount", true, "Trading with town vendors: the amount slider starts at exactly the amount that still gives happiness (thumbs up), and a button adds all liked goods in the right amount. You still confirm the deal yourself.");
            Celebrated = Config.Bind("Interface", "Celebrated5000", false, "Internal: the 5,000+ players thank-you banner was shown.");
            Respec = Config.Bind("Comfort", "Respec", false, "[Not fully vanilla] Refund talents, zombie perks and research. Talents and zombie perks: right click an unlocked node (controller: the button shown below it). Research: click a researched tech and choose Refund. Always asks first; nodes that depend on it are refunded too. Starter nodes and reputation research are never refunded, effects from buying (e.g. items) stay. Changes your save, backups are made automatically.");
            MoveSpeed = Config.Bind("Comfort", "MoveSpeed", 100, new ConfigDescription(
                "[Not fully vanilla] Walking speed of your character in percent (100 = normal). The game's own slowdowns (e.g. while aiming a bow) still apply, the walk animation speeds up to match. Only your character, not NPCs or zombies.",
                new AcceptableValueList<int>(100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200)));
            CraftMaxButton = Config.Bind("Comfort", "CraftMax", true, "Crafting: a Max button next to the amount sets it to as many as your ingredients allow (inventory and reachable chests, like the game counts). With a controller use the button shown on it.");
            ZombieRename = Config.Bind("Comfort", "ZombieRename", true, "Zombie window: a Rename button next to the name - type your own name or roll a new one at any time.");
            BackupCount = Config.Bind("Backups", "KeepBackups", 5, new ConfigDescription(
                "Before the game overwrites a save, the previous save is backed up (BepInEx/GK2VanillaPlus/Backups). Number of backups kept per save slot, 0 = off.",
                new AcceptableValueList<int>(0, 3, 5, 10, 20)));
            BackupMinutes = Config.Bind("Backups", "MinMinutesBetween", 10, new ConfigDescription(
                "Minimum minutes between two backups of the same slot (avoids a backup on every autosave).",
                new AcceptableValueList<int>(0, 5, 10, 15, 30, 60)));
            SaveKey = Config.Bind("Interface", "SaveKey", KeyboardShortcut.Empty, "Key for saving the game manually (empty = only the button in the mod menu).");
            MenuScale = Config.Bind("Interface", "MenuScale", 0, new ConfigDescription(
                "Size of the mod menu, FPS display and pinned list. 0 = automatic (follows the screen height).",
                new AcceptableValueList<int>(0, 80, 90, 100, 110, 125, 150, 175, 200)));
            MenuBg = Config.Bind("Interface", "MenuBackground", "Scene", new ConfigDescription(
                "Main menu background: the game's animated scene (Scene), one of six Vanilla+ pictures, or your own picture from your save (Mine).",
                new AcceptableValueList<string>("Scene", "Bg1", "Bg2", "Bg3", "Bg4", "Bg5", "Bg6", "Mine")));
            MenuBgStyle = Config.Bind("Interface", "MenuBackgroundStyle", "Gloomy", new ConfigDescription(
                "Color style of the main menu picture.",
                new AcceptableValueList<string>("Natural", "Gloomy", "Sepia", "Night", "Painting")));
            MenuBgBlur = Config.Bind("Interface", "MenuBackgroundBlur", 1, new ConfigDescription(
                "Blur of the main menu picture, so the menu stays in focus.",
                new AcceptableValueList<int>(0, 1, 2, 3)));
            MenuBgDim = Config.Bind("Interface", "MenuBackgroundDim", 25, new ConfigDescription(
                "Darkens the main menu picture in percent, so the buttons stay easy to read.",
                new AcceptableValueList<int>(0, 15, 25, 40, 55)));
            MenuBgFog = Config.Bind("Interface", "MenuBackgroundFog", 1, new ConfigDescription(
                "Slowly drifting fog over the main menu picture.",
                new AcceptableValueList<int>(0, 1, 2, 3)));
            MenuBgFogTone = Config.Bind("Interface", "MenuBackgroundFogTone", "FogDark", new ConfigDescription(
                "Fog color over the main menu picture: light mist or dark, gloomy fog.",
                new AcceptableValueList<string>("FogDark", "FogLight")));
            RainAmount = Config.Bind("Interface", "RainAmount", 100, new ConfigDescription(
                "Amount of rain particles in percent. Less rain helps on weak PCs and the Steam Deck, 0 turns the particles off (the weather itself stays).",
                new AcceptableValueList<int>(100, 75, 50, 25, 0)));
            WideRain = Config.Bind("Interface", "WideRain", true, "Rain covers the whole screen on ultrawide monitors and when zoomed out (the game only fills a 16:9 area).");
            HudCenter = Config.Bind("Interface", "HudCenter", false, "Ultrawide: move the HUD, area name, NPC window and the mod displays into the 16:9 area in the middle instead of the outer screen edges. The world stays ultrawide.");
            OledBlack = Config.Bind("Interface", "OledBlack", false, "Pure black instead of dark gray around the map (e.g. outside the church or at the level edge). Good for OLED screens.");
            HighContrast = Config.Bind("Interface", "HighContrast", false, "Stronger contrast: dark background for the pinned list, bold and brighter have/need numbers, larger tooltips.");
            ShotKey = Config.Bind("Screenshots", "Key", new KeyboardShortcut(KeyCode.F12), "Key for a screenshot (saved to BepInEx/GK2VanillaPlus/Screenshots).");
            ShotScale = Config.Bind("Screenshots", "Scale", 2, new ConfigDescription("Resolution multiplier: 2 = twice the screen resolution in each direction (e.g. 3840x2160 from 1920x1080).",
                new AcceptableValueList<int>(1, 2, 3, 4)));
            ShotHideHud = Config.Bind("Screenshots", "HideHud", true, "Hide the game HUD and the mod displays for the screenshot.");
            ShowOverlay = Config.Bind("Interface", "ShowOverlay", false, "Show the FPS display (toggle with F10). Position and contents: section [Overlay].");
            SafeStartGuard = Config.Bind("Interface", "SafeStartGuard", true, "Crash guard (safe start): if the game did not reach the main menu last time with Vanilla+ active (e.g. a black screen after a game update), the next start runs once without Vanilla+ (no patches or features, only this menu with F9) and tells you so. The start after that is normal again. Holding Left Shift while the game starts does the same once. Off = no guard.");
            Language = Config.Bind("Interface", "Language", "Auto", new ConfigDescription(
                "Language of the mod menu. Auto = game language (if a translation exists, otherwise English). Translations: BepInEx/GK2VanillaPlus/lang.",
                new AcceptableValueList<string>("Auto", "Deutsch", "English", "Français", "Español", "Português", "Русский", "中文")));
            LastSeenVersion = Config.Bind("Interface", "LastSeenVersion", "", "Internal: mod version whose changelog was last shown (the 'What's new' window appears once after an update).");
            OvCorner = Config.Bind("Overlay", "Corner", "BottomLeft", new ConfigDescription("Screen corner of the FPS display.",
                new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
            OvLayout = Config.Bind("Overlay", "Layout", "Row", new ConfigDescription("Row = all values in one line. Column = one value per line.",
                new AcceptableValueList<string>("Row", "Column")));
            OvOrder = Config.Bind("Overlay", "Order", OverlayOrder.Default, "Order of the values in the FPS display (comma-separated, set it with the arrows in the mod menu).");
            OvSeparator = Config.Bind("Overlay", "Separator", "None", new ConfigDescription("Separator between the values when they are shown in one line. Set the order of the values below with the arrows.",
                new AcceptableValueList<string>("None", "Dash", "Bar", "Dot")));
            OvFps = Config.Bind("Overlay", "Fps", true, "Frames per second (average over 0.5 s).");
            OvLows = Config.Bind("Overlay", "Lows", true, "1% low FPS: how smooth it feels. Close to the FPS value = no stutter.");
            OvFrameTime = Config.Bind("Overlay", "FrameTime", false, "Frame time in ms (average and slowest frame).");
            OvCpu = Config.Bind("Overlay", "Cpu", false, "CPU load of the game (100 % = all cores busy).");
            OvGpu = Config.Bind("Overlay", "Gpu", false, "GPU load (3D engine, like Task Manager). Windows only.");
            OvGpuTemp = Config.Bind("Overlay", "GpuTemp", false, "GPU temperature in °C. NVIDIA graphics cards only (AMD/Intel and the CPU temperature can't be read without an extra system driver). Not under Mac/Linux.");
            OvRam = Config.Bind("Overlay", "Ram", false, "RAM used by the game / installed RAM.");
            OvVram = Config.Bind("Overlay", "Vram", false, "Video memory used by the game's textures and buffers / GPU memory.");
            OvResolution = Config.Bind("Overlay", "Resolution", false, "Current render resolution.");
            OvClock = Config.Bind("Overlay", "Clock", false, "Current time.");
            OvWeekday = Config.Bind("Overlay", "Weekday", false, "In-game weekday (Pride, Sloth, ...).");
            OvGameTime = Config.Bind("Overlay", "GameTime", false, "In-game time of day.");
            PinsEnabled = Config.Bind("Pins", "Enabled", true, "Pin recipes, blueprints and town buildings (pin icon in their top right corner). Pinned items show have/need counts.");
            PinsChests = Config.Bind("Pins", "Chests", "Area", new ConfigDescription("Which items count as 'have': only your inventory, your inventory and the chests in the area you are in (like the game's crafting), or additionally all chests on the map.",
                new AcceptableValueList<string>("Inventory", "Area", "Everywhere")));
            PinsNotify = Config.Bind("Pins", "NotifyReady", true, "Short message with a sound when a pinned item becomes ready (you have everything) or a pinned quest is done.");
            PinsAutoUnpin = Config.Bind("Pins", "AutoUnpin", true, "Unpin a recipe automatically when you start crafting it.");
            PinsVariants = Config.Bind("Pins", "RecipeVariants", true, "Under a pinned recipe: the workbench and, if the item can be made in several ways, < > to switch between the recipes (only known recipes).");
            PinsTree = Config.Bind("Pins", "IngredientTree", true, "Ingredients you can craft yourself get a + that shows their own ingredients (up to 3 levels).");
            PinsKey = Config.Bind("Pins", "PinKey", new KeyboardShortcut(KeyCode.Mouse0), "How to pin/unpin: Mouse0 (default) = left click on the pin icon. Any other key or mouse button (middle, side buttons): point the mouse at a recipe, order or blueprint and press it - clicking the pin icon then does nothing (no accidental pins). The right mouse button is not possible (it closes the game window).");
            PinsPadButton = Config.Bind("Pins", "ControllerButton", "RT", "Controller: hold this button during normal play to navigate the pinned list (only while no game window is open). Click the field and press any controller button; Esc = off.");
            PinsFuel = Config.Bind("Pins", "ShowFuel", true, "Show the fuel a recipe needs from its workbench (e.g. a furnace) as an extra line.");
            PinsCorner = Config.Bind("Pins", "Corner", "TopRight", new ConfigDescription("Screen corner of the pinned list.",
                new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
            // PinsClick (Rechtsklick zum Anpinnen) kommt mit 1.8 - Rechtsklick schliesst im Spiel das Fenster
            PinsSize = Config.Bind("Pins", "Size", "Medium", new ConfigDescription("Text and icon size of the pinned list.",
                new AcceptableValueList<string>("Small", "Medium", "Large", "ExtraLarge")));
#if MINIMAP
            MinimapEnabled = Config.Bind("Minimap", "Enabled", false, "Small map in a screen corner: a section of the game's own world map around you (outdoors; indoors your location on the map).");
            MinimapCorner = Config.Bind("Minimap", "Corner", "BottomRight", new ConfigDescription("Screen corner of the minimap.",
                new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
            MinimapSize = Config.Bind("Minimap", "Size", 220, new ConfigDescription("Size of the minimap.",
                new AcceptableValueList<int>(160, 190, 220, 260, 300, 360)));
            MinimapZoom = Config.Bind("Minimap", "Zoom", 100, new ConfigDescription("Zoom of the minimap in percent (higher = closer).",
                new AcceptableValueList<int>(50, 75, 100, 150, 200, 300)));
            MinimapKey = Config.Bind("Minimap", "Key", KeyboardShortcut.Empty, "Key to show or hide the minimap (empty = only in the mod menu).");
#endif
            LastGameVersion = Config.Bind("Interface", "LastGameVersion", "", "Internal: game version at the last start (safe mode shows a note after game updates).");
#if !NEXUS
            RatePromptSessions = Config.Bind("Interface", "RatePromptSessions", 0, "Internal: number of game starts (for the Steam rating prompt).");
            RatePromptNextAt = Config.Bind("Interface", "RatePromptNextAt", 3, "Internal: session count at which the Steam rating prompt is shown next.");
            RatePromptDone = Config.Bind("Interface", "RatePromptDone", false, "Internal: the Steam rating prompt was answered (rated or dismissed) and won't be shown again.");
#endif
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
            BenchShotSet = Config.Bind("Benchmark", "ShotSet", "", "steam = Screenshot-Tour fuer die Store-Seite (ein Bild je Feature).");
            BenchShotRes = Config.Bind("Benchmark", "ShotResolution", "", "Aufloesung fuer die Screenshot-Tour, z. B. 1920x1080 (leer = unveraendert).");
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
                case "Camera": CameraZoom.OnSettingsChanged(); break;
                case "Comfort": CameraZoom.OnSettingsChanged(); Application.runInBackground = true; ModsButton.ApplyVisibility(); break;
            }
        }

        // Klein gehalten: Crash-Waechter und Logo-Schnelllauf (stellt die Zeitskala zurueck) muessen jedes Frame laufen.
        // Der grosse Rest steckt in UpdateCore (eigene Methode, nicht inline): scheitert dessen JIT nach einem Spiel-Update
        // an einem Typ, wirft erst der Aufruf hier - SafeStart.Tick und SkipLogos laufen trotzdem.
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            try { SafeStart.Tick(); } catch { }
            SafeMode.Run("SkipLogos", SkipLogosPatch.Tick);
            if (Gui == null) return;
            try
            {
                if (SafeMode.AllOff) UpdateVanilla(dt);
                else UpdateCore(dt);
            }
            catch (Exception e) { SafeMode.Report("Update", e); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void UpdateCore(float dt)
        {
            // Tasten: bewusst try/catch statt SafeMode.Run - F9 muss auch nach wiederholten Fehlern weiter funktionieren
            try
            {
                if (MenuKey.Value.IsDown()) Gui.ToggleMenu();
                if (OverlayKey.Value.IsDown()) ShowOverlay.Value = !ShowOverlay.Value;
                if (SaveKey.Value.MainKey != KeyCode.None && SaveKey.Value.IsDown()) ManualSave.Save(Gui.MenuOpen);
                if (WeekPlanKey.Value.MainKey != KeyCode.None && WeekPlanKey.Value.IsDown()) Gui.ToggleWeekPlan();
                if (HudKey.Value.MainKey != KeyCode.None && HudKey.Value.IsDown()) HudToggle.Toggle();
#if MINIMAP
                if (MinimapKey.Value.MainKey != KeyCode.None && MinimapKey.Value.IsDown()) MinimapEnabled.Value = !MinimapEnabled.Value;
#endif
            }
            catch (Exception e) { SafeMode.Report("Keys", e); }
            SafeMode.Run("Hud", HudToggle.Tick);
            SafeMode.Run("Graphics", () => GraphicsBench.Tick(dt));
            SafeMode.Run("Pins", Pins.Tick);
            SafeMode.Run("Pins", BuildPinScan.Tick);
            SafeMode.Run("Oled", Oled.Tick);
            SafeMode.Run("Rain", Rain.Tick);
            SafeMode.Run("HudCenter", GK2Tweaks.HudCenter.Tick);
            SafeMode.Run("HudClock", GK2Tweaks.HudClock.Tick);
            SafeMode.Run("EscLeave", () => GK2Tweaks.EscLeave.Tick(Gui.AnyWindowOpen));
            SafeMode.Run("Pins", PadBind.Tick);
            SafeMode.Run("MoveObjects", BuildMove.Tick);
#if MINIMAP
            SafeMode.Run("Minimap", Minimap.Tick);
#endif
            SafeMode.Run("News", NewsTick);
#if !NEXUS
            SafeMode.Run("News", RateTick);
#endif
            // Mod-Menue: wie Tasten nie abschalten, nur melden
            try { Gui.Tick(dt); } catch (Exception e) { SafeMode.Report("Gui", e); }
            SafeMode.Run("Api", VanillaPlusApi.Tick);

            try
            {
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
            }
            catch (Exception e) { SafeMode.Report("Stats", e); }
#if DEV
            bench?.Update(dt);
#endif
            SafeMode.Run("Saves", AutoSave.Tick);
            SafeMode.Run("Zoom", ZoomPatch.Tick);
            SafeMode.Run("Zoom", () => CameraZoom.Tick(dt));
            SafeMode.Run("Screenshots", HiResShot.Tick);
            SafeMode.Run("Ultrawide", MenuSideFill.Tick);
            SafeMode.Run("MenuBackground", MenuBackground.Tick);
#if DEV
            UiDump.Tick();
#endif
        }

        // Sicherer Start: nur Mod-Menue (F9) und FPS-Anzeige (F10)
        private void UpdateVanilla(float dt)
        {
            try
            {
                if (MenuKey.Value.IsDown()) Gui.ToggleMenu();
                if (OverlayKey.Value.IsDown()) ShowOverlay.Value = !ShowOverlay.Value;
            }
            catch (Exception e) { SafeMode.Report("Keys", e); }
            try { Gui.Tick(dt); } catch (Exception e) { SafeMode.Report("Gui", e); }
        }

        private bool newsChecked;
        private float menuSince = -1f;

        // "Was ist neu?" einmal nach einem Update, sobald das Hauptmenue ein paar Sekunden steht
        private void NewsTick()
        {
            if (newsChecked || BenchOn) return;
            MainGame mg = MainGame.Instance;
            if (mg == null || mg.gameState != MainGame.GameState.MainMenu) { menuSince = -1f; return; }
            if (menuSince < 0f) { menuSince = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - menuSince < 3f) return;
            newsChecked = true;
            if (SafeMode.NoticePending) { SafeMode.NoticePending = false; ManualSave.Toast(SafeMode.UpdateNotice(), 10f); }
            bool news = Changelog.ShouldAutoShow();
            if (!Celebrated.Value) { Gui.ShowCelebration(news ? (Action)(() => Gui.ShowNews(true)) : null); return; }
            if (news) Gui.ShowNews(true);
        }

#if !NEXUS
        private bool rateChecked;
        private float rateMenuSince = -1f;

        // Bewertungshinweis: nach ein paar Spielsitzungen, nie zusammen mit "Was ist neu?" oder einem anderen Mod-Fenster
        private void RateTick()
        {
            if (rateChecked || BenchOn) return;
            if (RatePromptDone.Value || RatePromptSessions.Value < RatePromptNextAt.Value) { rateChecked = true; return; }
            MainGame mg = MainGame.Instance;
            if (mg == null || mg.gameState != MainGame.GameState.MainMenu) { rateMenuSince = -1f; return; }
            if (rateMenuSince < 0f) { rateMenuSince = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - rateMenuSince < 6f) return;
            rateChecked = true;
            if (Gui.AnyWindowOpen) return; // z.B. "Was ist neu?" ist offen -> erst in der naechsten Sitzung erneut versuchen
            Gui.ShowRatePrompt();
        }
#endif

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
            }
            ApplyPacing();
        }

        // Steam Deck / Linux (Proton) erkennen
        internal static bool IsDeck => Environment.GetEnvironmentVariable("SteamDeck") == "1";
        internal static bool IsLinux => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH")) || IsDeck;
        // Auto schuetzt jetzt auch Windows: hat das Spiel selbst VSync an, laesst Vanilla+ es nie aus (Tearing-Meldung Bonjour, 1.7.6)
        internal static bool TearFix => NoTearing.Value == "On" || (NoTearing.Value == "Auto" && (IsLinux || GameWantsVsync));

        // Was das Spiel zuletzt selbst gesetzt hat (gelesen in ScreenSettingsPatch direkt nach dem Original, -1 = noch unbekannt).
        // Nur dort lesen, nie in ApplyPacing - sonst wuerden die eigenen Werte als "Spielwahl" gemerkt.
        internal static int GameVsync = -1, GameFps = -1;
        // vSyncCount, den ApplyPacing zuletzt hinterlassen hat (-1 = noch nie). ScreenSettingsPatch erkennt damit,
        // ob nach ApplyScreenSettings noch der eigene Wert steht (Spiel hat vSyncCount nicht geschrieben).
        internal static int OwnVsync = -1;
        internal static bool GameWantsVsync => GameVsync > 0 || GameVsyncSettingOn();

        // VSync-Schalter aus dem Spielmenue per Reflection (Name unbekannt): nur exakte Namen aus einer Liste
        // (siehe FindGameVsyncMember), damit Hilfsflags wie "vsyncSupported" nicht als Einstellung gelesen werden.
        // Nicht gefunden -> false, dann zaehlt nur vSyncCount wie bisher.
        private static System.Reflection.MemberInfo gameVsyncMember;
        private static bool gameVsyncSearched, gameVsyncErrorLogged;

        internal static bool GameVsyncSettingOn()
        {
            try
            {
                GameSettings gs = GameSettings.Instance;
                if (gs == null) return false;
                if (!gameVsyncSearched)
                {
                    gameVsyncSearched = true;
                    gameVsyncMember = FindGameVsyncMember();
                    if (gameVsyncMember != null) Log.LogInfo("Game VSync setting found: GameSettings." + gameVsyncMember.Name);
                    else Log.LogInfo("Game VSync setting not found, using vSyncCount only");
                }
                if (gameVsyncMember == null) return false;
                object v = null;
                System.Reflection.FieldInfo f = gameVsyncMember as System.Reflection.FieldInfo;
                if (f != null) v = f.GetValue(gs);
                System.Reflection.PropertyInfo p = gameVsyncMember as System.Reflection.PropertyInfo;
                if (p != null) v = p.GetValue(gs, null);
                return v is bool && (bool)v;
            }
            catch (Exception ex)
            {
                if (!gameVsyncErrorLogged) { gameVsyncErrorLogged = true; Log.LogWarning("Game VSync setting could not be read: " + ex.Message); }
                gameVsyncMember = null;
                return false;
            }
        }

        // Erlaubte Namen (ohne Gross-/Kleinschreibung, nach Entfernen von "_", "m_" und Backing-Field-Klammern)
        private static readonly string[] GameVsyncNames = { "vsync", "isvsync", "vsyncenabled", "isvsyncenabled", "usevsync", "enablevsync", "vsyncon" };

        private static string NormVsyncName(string n)
        {
            if (n.StartsWith("<", StringComparison.Ordinal))
            {
                int e = n.IndexOf('>');
                if (e > 1) n = n.Substring(1, e - 1);   // <VSync>k__BackingField -> VSync
            }
            if (n.StartsWith("m_", StringComparison.Ordinal)) n = n.Substring(2);
            return n.TrimStart('_').ToLowerInvariant();
        }

        private static System.Reflection.MemberInfo FindGameVsyncMember()
        {
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            string other = "";
            System.Reflection.MemberInfo best = null;
            bool bestPublic = false;
            // Properties zuerst (oeffentliche Einstellung vor privatem Hilfsfeld), dann Felder; oeffentlich vor nicht-oeffentlich
            foreach (System.Reflection.PropertyInfo p in typeof(GameSettings).GetProperties(bf))
            {
                if (p.Name.IndexOf("vsync", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool ok = p.PropertyType == typeof(bool) && p.CanRead && p.GetIndexParameters().Length == 0
                    && Array.IndexOf(GameVsyncNames, NormVsyncName(p.Name)) >= 0;
                if (!ok) { other += " " + p.Name + ":" + p.PropertyType.Name; continue; }
                System.Reflection.MethodInfo g = p.GetGetMethod(true);
                bool pub = g != null && g.IsPublic;
                if (best == null || (pub && !bestPublic)) { best = p; bestPublic = pub; }
            }
            foreach (System.Reflection.FieldInfo f in typeof(GameSettings).GetFields(bf))
            {
                if (f.Name.IndexOf("vsync", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool ok = f.FieldType == typeof(bool) && Array.IndexOf(GameVsyncNames, NormVsyncName(f.Name)) >= 0;
                if (!ok) { other += " " + f.Name + ":" + f.FieldType.Name; continue; }
                if (best == null || (f.IsPublic && !bestPublic)) { best = f; bestPublic = f.IsPublic; }
            }
            // Diagnose: VSync-Mitglieder, die nicht auf der Liste stehen (anderer Name/Typ), nur loggen, nicht auswerten
            if (other.Length > 0) Log.LogInfo("Game VSync members (not on the name list or not bool, ignored):" + other);
            if (best != null) Log.LogInfo("Game VSync member chosen by exact name: " + best.Name + (bestPublic ? " (public)" : " (non-public)"));
            return best;
        }

        // Entscheidung nur bei Aenderung loggen (ApplyScreenSettings laeuft oft)
        private static string lastPacingLog;
        private static void LogPacing(string msg)
        {
            if (msg == lastPacingLog) return;
            lastPacingLog = msg;
            Log.LogInfo(msg);
        }

        internal static void ApplyPacing()
        {
            string mode = Pacing.Value;
            if (mode == "Game")
            {
                // Das Spiel schaltet VSync ab, sobald ein FPS-Limit unter der Bildwiederholrate gesetzt ist -> Tearing (v. a. Steam Deck)
                if (TearFix && QualitySettings.vSyncCount == 0)
                {
                    int hz0 = MonitorHz(), lim = Application.targetFrameRate;
                    // Nur der neue Windows-Weg (Auto, kein Linux): VSync nur behalten, wenn das FPS-Limit des Spiels
                    // ungefaehr ein Teiler der Bildwiederholrate ist - sonst wuerde das Limit ueberschritten
                    // (z. B. Limit 45 bei 60 Hz -> 60 FPS, Limit 100 bei 144 Hz -> 144 FPS). Dann gilt das exakte Limit des Spiels.
                    bool windowsAuto = NoTearing.Value == "Auto" && !IsLinux;
                    float ratio = (lim > 0 && hz0 > 0) ? (float)hz0 / lim : 0f;
                    bool divisor = lim <= 0 || hz0 <= 0 || lim >= hz0 || Mathf.Abs(ratio - Mathf.Round(ratio)) < 0.05f;
                    if (windowsAuto && !divisor)
                    {
                        LogPacing($"Tearing fix skipped: game limit {lim} is no even split of {hz0} Hz, keeping the game's exact limit (set No tearing = On to force VSync)");
                    }
                    else
                    {
                        QualitySettings.vSyncCount = (lim > 0 && hz0 > 0) ? Mathf.Clamp(Mathf.RoundToInt(ratio), 1, 4) : 1;
                        Application.targetFrameRate = -1;
                        LogPacing($"Tearing fix: VSync kept on (vSyncCount {QualitySettings.vSyncCount}, monitor {hz0} Hz, game limit {lim})");
                    }
                }
                OwnVsync = QualitySettings.vSyncCount;
                return;
            }
            int fps = TargetFps.Value;
            bool gameVsync = GameWantsVsync;   // nur fuer die Log-Notiz
            string note = "";
            // Kein Tearing = Aus gibt wie in 1.7.5 das exakte Software-Limit; Auto enthaelt ueber TearFix schon die VSync-Wahl des Spiels
            if (mode == "Limit" && !TearFix)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = fps > 0 ? fps : -1;
            }
            else
            {
                // VSync-Modus, Kein-Tearing, oder Limit bei VSync im Spiel: Obergrenze ueber das VSync-Intervall
                // (Unity ignoriert targetFrameRate, solange vSyncCount > 0 ist - daher nur Teiler der Bildwiederholrate)
                int hz = MonitorHz();
                int interval;
                if (mode == "Limit")
                    // Limit: nie unter das Ziel fallen (90 Hz / 60 -> 90 FPS statt 45, 165 Hz / 60 -> 82 statt 55)
                    interval = (fps > 0 && hz > 0) ? Mathf.Clamp(Mathf.FloorToInt((float)hz / fps + 0.001f), 1, 4) : 1;
                else
                    interval = (fps > 0 && hz > 0) ? Mathf.Clamp(Mathf.RoundToInt((float)hz / fps), 1, 4) : 1;
                QualitySettings.vSyncCount = interval;
                Application.targetFrameRate = -1;
                if (mode == "Limit")
                    note = $" | Limit with {(NoTearing.Value == "On" ? "No tearing = On" : gameVsync ? "game VSync on" : "no-tearing fix")}: keeping VSync, interval {interval} (~{(hz > 0 ? hz / interval : 0)} FPS)";
            }
            OwnVsync = QualitySettings.vSyncCount;
            // eine Zeile pro Entscheidung, damit die Wiederholungs-Sperre in LogPacing greift
            LogPacing($"Bildrate: {mode}, Ziel {fps}, vSyncCount {QualitySettings.vSyncCount}, targetFrameRate {Application.targetFrameRate}, Monitor {MonitorHz()} Hz, game vSyncCount {GameVsync}{note}");
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
