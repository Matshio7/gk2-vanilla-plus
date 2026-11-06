using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Sicherer Modus: Nach einem Spiel-Update koennen Felder/Methoden des Spiels fehlen oder sich anders verhalten.
    // 1. Beim Start wird geprueft, ob alles, worauf eine Funktion zugreift, noch existiert - sonst wird nur diese
    //    Funktion abgeschaltet (statt Fehler zu werfen), der Rest des Mods laeuft weiter.
    // 2. Wirft eine Funktion im laufenden Spiel wiederholt Fehler, wird sie ebenfalls abgeschaltet.
    // 3. Hat sich die Spielversion geaendert, gibt es einen kurzen Hinweis mit dem Ergebnis der Pruefung.
    // 4. Sicherer Start (SafeStart): kam das Spiel beim letzten Start nicht ins Hauptmenue, ist fuer diesen einen
    //    Start alles aus (AllOff) - On() liefert dann fuer jede Funktion false, Run() fuehrt nichts aus.
    internal static class SafeMode
    {
        private const int MaxErrors = 5;
        private static readonly Dictionary<string, int> errors = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> disabled = new Dictionary<string, string>();
        internal static IEnumerable<string> Disabled => disabled.Keys;
        internal static int DisabledCount => disabled.Count;
        // Funktionen, die das Spiel inzwischen selbst kann (Objekte verschieben ab 1.011): bleiben aus (keine Patches),
        // gelten aber nicht als "wegen Spiel-Update abgeschaltet" - kein Hinweis, kein "bitte auf ein Mod-Update warten".
        private static readonly HashSet<string> retired = new HashSet<string>();
        private static int ShownCount() { int n = 0; foreach (string f in disabled.Keys) if (!retired.Contains(f)) n++; return n; }
        internal static bool NoticePending;
        private static string gameFrom, gameTo;
        // Sicherer Start: dieser Start laeuft ohne jede Funktion und ohne Patches (nur Mod-Menue und Log)
        internal static bool AllOff { get; private set; }

        // Anzeigenamen (Deutsch, Englisch)
        private static readonly Dictionary<string, string[]> names = new Dictionary<string, string[]>
        {
            { "Pins", new[] { "Anpinnen", "Pinning" } },
            { "Rain", new[] { "Regen über ganze Breite", "Full-width rain" } },
            { "Minimap", new[] { "Minimap", "Minimap" } },
            { "WeekPlan", new[] { "Wochenplan", "Week plan" } },
            { "ModsButton", new[] { "Mods-Button", "Mods button" } },
            { "SkipLogos", new[] { "Intro-Logos überspringen", "Skip intro logos" } },
            { "MenuInfo", new[] { "Versionshinweis im Hauptmenü", "Version note in main menu" } },
            { "Graphics", new[] { "Grafik-Einstellungen", "Graphics settings" } },
            { "Saves", new[] { "Speichern & Backups", "Saving & backups" } },
            { "Zoom", new[] { "Kamera-Zoom", "Camera zoom" } },
            { "Oled", new[] { "OLED-Schwarz", "OLED black" } },
            { "HudCenter", new[] { "HUD zur Mitte", "HUD to the center" } },
            { "Screenshots", new[] { "Screenshots", "Screenshots" } },
            { "Ultrawide", new[] { "Ultrawide-Hauptmenü", "Ultrawide main menu" } },
            { "Hud", new[] { "HUD ausblenden", "Hide HUD" } },
            { "WorkshopUpload", new[] { "Workshop-Upload", "Workshop upload" } },
            { "InstantRemove", new[] { "Sofort abbauen", "Instant removal" } },
            { "HudClock", new[] { "Tag & Uhrzeit am HUD", "Day & time on HUD" } },
            { "EscLeave", new[] { "Gespräch mit Esc/B verlassen", "Leave conversations with Esc/B" } },
            { "Transitions", new[] { "Schnellere Übergänge", "Faster transitions" } },
            { "MoveObjects", new[] { "Objekte verschieben", "Move objects" } },
            { "TradeLikes", new[] { "Handel: passende Menge", "Trade: right amount" } },
            { "ZombieRename", new[] { "Zombies umbenennen", "Rename zombies" } },
            { "MenuBackground", new[] { "Hauptmenü-Hintergrund", "Main menu background" } },
            { "Respec", new[] { "Talente & Forschung zurückerstatten", "Refund talents & research" } },
            { "CraftMax", new[] { "Herstellen: Max-Knopf", "Crafting: Max button" } },
            { "CraftRepair", new[] { "Hängende Werkbänke lösen", "Fix stuck workbenches" } },
            { "MoveSpeed", new[] { "Laufgeschwindigkeit", "Movement speed" } },
            { "UiScale", new[] { "HUD & Menüs: Größe", "HUD & menus: size" } },
            { "QuestItems", new[] { "Quest-Pins: benötigte Gegenstände", "Quest pins: required items" } },
            { "RepeatWork", new[] { "Arbeit wiederholen", "Repeat work" } },
            { "Gui", new[] { "Mod-Menü & Anzeigen", "Mod menu & displays" } },
            { "Keys", new[] { "Tastenkürzel", "Hotkeys" } },
            { "News", new[] { "Hinweisfenster (Neuigkeiten, Bewertung)", "Notice windows (news, rating)" } },
            { "Api", new[] { "Schnittstelle für andere Mods", "Interface for other mods" } },
            { "Stats", new[] { "FPS-Protokoll", "FPS log" } },
        };

        internal static string Name(string f) => names.TryGetValue(f, out string[] n) ? Labels.T(n[0], n[1]) : f;

        // Harmony-Patchklasse -> Funktion
        internal static string FeatureOf(Type patch)
        {
            switch (patch.Name)
            {
                case "OrderPinPatch": case "AlchemyPinPatch": case "CraftCellPinPatch": case "SelectionPinPatch": case "QuestPinPatch": return "Pins";
                case "LongNotes": return "WeekPlan";
                case "MainMenuModsButtonPatch": case "PauseModsButtonPatch": return "ModsButton";
                case "ModdedLabelPatch": return "MenuInfo";
                case "ZoomPatch": return "Zoom";
                case "BackupPatch": case "SaveBlockPatch": case "BattleSavePatch": return "Saves";
                case "TierPatch": case "ScreenSettingsPatch": return "Graphics";
                case "InstantRemovePatch": return "InstantRemove";
                case "CraftRepairPatch": return "CraftRepair";
                case "RainScanPatch": return "Rain";
                case "MoveSpeedPatch": return "MoveSpeed";
                case "UiScalePatch": return "UiScale";
                case "MoveInputPatch": case "MoveTargetPatch": case "MoveCellsPatch": case "MoveBuildPatch": case "MoveDisablePatch": case "MoveRemoveLabelPatch": return "MoveObjects";
                case "TradePressPatch": case "TradeCountPatch": return "TradeLikes";
                case "RespecOverPatch": case "RespecOutPatch": case "RespecTechPatch": return "Respec";
                case "ZoneRedrawPatch": return "HudClock";
                case "TeleportPatch": case "FadeInPatch": case "FadeOutPatch": case "CleanupPatch": return "Transitions";
                default: return patch.Name;
            }
        }

        internal static bool On(string feature) => !AllOff && !disabled.ContainsKey(feature);

        internal static void EnterVanillaStart()
        {
            AllOff = true;
            NoticePending = false;
        }

        internal static void Run(string feature, Action a)
        {
            if (AllOff || disabled.ContainsKey(feature)) return;
            try { a(); }
            catch (Exception e) { Fail(feature, e); }
        }

        internal static void Fail(string feature, Exception e)
        {
            errors.TryGetValue(feature, out int n);
            errors[feature] = ++n;
            if (n == 1) Plugin.Log.LogWarning("[" + feature + "] " + e);
            else Plugin.Log.LogWarning("[" + feature + "] " + e.GetType().Name + ": " + e.Message + " (" + n + ")");
            if (n >= MaxErrors) Disable(feature, "repeated errors: " + e.Message, true);
        }

        // Fuer Teile, die nie abgeschaltet werden (Tasten, Mod-Menue, FPS-Protokoll, Update-Schleife): nur zaehlen und
        // loggen - kein Disable, kein Hinweis "wurde abgeschaltet" (der waere falsch, der Code laeuft ja weiter).
        // Das Log ist gedeckelt, damit ein Fehler in jedem Frame das Log nicht fuellt.
        private const int MaxReportLogs = 10;
        internal static void Report(string feature, Exception e)
        {
            try
            {
                errors.TryGetValue(feature, out int n);
                errors[feature] = ++n;
                if (n == 1) Plugin.Log.LogWarning("[" + feature + "] " + e);
                else if (n <= MaxReportLogs) Plugin.Log.LogWarning("[" + feature + "] " + e.GetType().Name + ": " + e.Message + " (" + n + ")");
                else if (n == MaxReportLogs + 1) Plugin.Log.LogWarning("[" + feature + "] more errors - not logged any more this session");
            }
            catch { }
        }

        internal static void Disable(string feature, string reason, bool notify)
        {
            if (disabled.ContainsKey(feature)) return;
            disabled[feature] = reason;
            Plugin.Log.LogWarning("Safe mode: '" + feature + "' disabled – " + reason);
            if (notify)
                ManualSave.Toast(string.Format(Labels.T("Vanilla+: „{0}“ wurde abgeschaltet (Fehler, evtl. nach einem Spiel-Update).",
                    "Vanilla+: \"{0}\" was turned off (errors, possibly after a game update)."), Name(feature)), 6f);
        }

        // ---------- Patches anwenden ----------
        // Jeder Patch einzeln: die Liste enthaelt Lambdas (() => typeof(X)), damit auch ein Typ, der schon beim
        // Aufloesen scheitert (TypeLoadException/TypeInitializationException), nur seinen eigenen Eintrag trifft.
        // Der aeussere try/catch faengt alles ab, was die Schleife selbst abbrechen koennte - die restlichen
        // Startschritte in Plugin.Awake laufen in jedem Fall weiter.
        internal static void ApplyPatches(Harmony harmony, List<Func<Type>> patches)
        {
            int index = 0, applied = 0, skipped = 0, failed = 0;
            try
            {
                foreach (Func<Type> get in patches)
                {
                    index++;
                    Type t = null;
                    string feature = null;
                    try
                    {
                        t = get();
                        feature = FeatureOf(t);
                        if (!On(feature)) { skipped++; Plugin.Log.LogWarning("Patch " + t.Name + " skipped (safe mode: " + feature + ")"); continue; }
                        harmony.CreateClassProcessor(t).Patch();
                        applied++;
                    }
                    catch (Exception e)
                    {
                        failed++;
                        string name = t != null ? t.Name : "#" + index;
                        Plugin.Log.LogError("Patch " + name + " fehlgeschlagen: " + e.GetType().Name + ": " + e.Message);
                        if (feature != null) Disable(feature, "patch failed: " + e.Message, false);
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogError("Patch loop aborted at entry " + index + ": " + e); }
            Plugin.Log.LogInfo("Patches: " + applied + " applied, " + skipped + " skipped, " + failed + " failed");
        }

        // ---------- Pruefung beim Start ----------
        private sealed class Check { public string Feature, Member; public Func<Type> Type; public Func<Type, bool> Test; }
        private static readonly List<Check> checks = new List<Check>();
        // Der Typ steckt bewusst in einem Lambda (() => typeof(X)): fehlt nach einem Spiel-Update ein ganzer Typ,
        // scheitert so nur diese eine Pruefung. Ein direktes typeof(X) hier im Methodenrumpf wuerde schon beim
        // JIT-Kompilieren von CheckGame eine TypeLoadException werfen und den kompletten Mod beim Start mitreissen.
        private static void F(string feature, Func<Type> type, string field) =>
            checks.Add(new Check { Feature = feature, Type = type, Member = field, Test = t => AccessTools.Field(t, field) != null || AccessTools.Property(t, field) != null });
        private static void M(string feature, Func<Type> type, string method) =>
            checks.Add(new Check { Feature = feature, Type = type, Member = method + "()", Test = t => HasMethod(t, method) });

        // AccessTools.Method wirft bei ueberladenen Methoden (z. B. UIBasicFade.FadeIn) AmbiguousMatchException
        private static bool HasMethod(Type t, string name)
        {
            for (Type c = t; c != null; c = c.BaseType)
                foreach (System.Reflection.MethodInfo m in c.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
                    if (m.Name == name) return true;
            return false;
        }

        internal static void CheckGame()
        {
            F("Pins", () => typeof(UICraftPreviewItemCell), "data");
            F("Pins", () => typeof(UIBuildingWindow), "displayedBuildItemGUIs");
            F("Pins", () => typeof(UIBuildingWidget), "data");
            F("Pins", () => typeof(UITownBuildingWindow), "displayedBuildItemGUIs");
            F("Pins", () => typeof(UITownBuildingWidget), "data");
            F("Pins", () => typeof(UIBaseCraftSelectionWindow), "headerLabel");
            F("Pins", () => typeof(UIQuestInfoWindow), "data");
            F("Pins", () => typeof(UIQuestInfoWindow), "header");
            F("Rain", () => typeof(WeatherComponent), "parameters");
            F("Rain", () => typeof(CPParticleEmission), "defaultValue");
            M("Rain", () => typeof(WeatherComponent), "Awake");
            M("WeekPlan", () => typeof(UINotificator), "ShowNotification");
            F("WeekPlan", () => typeof(UIHUDWheel), "dayIcons");
            F("ModsButton", () => typeof(UIMainMenuWindow), "gameSettingsButton");
            F("ModsButton", () => typeof(UIGamePauseWindow), "settingsBtn");
            M("SkipLogos", () => typeof(LazyBearTechnology.Preloader.LazyPreloader), "RunLogoCoroutine");
            F("SkipLogos", () => typeof(LazyBearTechnology.Preloader.LazyPreloader), "logoList");
            F("SkipLogos", () => typeof(LazyBearTechnology.Preloader.LazyPreloader), "videoPlayer");
            F("MenuInfo", () => typeof(UIMainMenuInfoPanel), "versionLabel");
            M("WorkshopUpload", () => typeof(SteamWorkshopCreatorService), "SubmitContent");
            M("InstantRemove", () => typeof(Wgo), "DoBuildRemove");
            M("InstantRemove", () => typeof(CraftComponent), "TryFinishCurCraft");
            F("InstantRemove", () => typeof(CraftComponent), "IsDestroyingCraftActive");
            F("HudClock", () => typeof(WorldZoneWidget), "worldZoneLabel");
            F("HudClock", () => typeof(EnvironmentData), "Day");
            F("HudClock", () => typeof(EnvironmentData), "TimeOfDay");
            M("MoveObjects", () => typeof(Wgo), "TryRegisterWorkbenchExtensionDelayed");
            F("EscLeave", () => typeof(UIMultiAnswer), "multiAnswers");
            F("EscLeave", () => typeof(UIMultiAnswer), "visualData");
            F("EscLeave", () => typeof(UIMultiAnswer), "interactable");
            M("EscLeave", () => typeof(UIMultiAnswer), "OnAnswerSelect");
            M("Transitions", () => typeof(PlayerController), "Teleport");
            M("Transitions", () => typeof(UIBasicFade), "FadeIn");
            M("Transitions", () => typeof(UIBasicFade), "FadeOut");
            M("Transitions", () => typeof(MainGame), "HiddenOptimization");
            F("Transitions", () => typeof(TeleportDataBase), "delayInFade");
            M("MoveObjects", () => typeof(BuildController), "UpdateBuildModeInput");
            F("MoveObjects", () => typeof(BuildController), "buildPointer");
            F("MoveObjects", () => typeof(BuildController), "isBuildModeInputLocked");
            F("MoveObjects", () => typeof(RemovePointer), "currentRemovingSelection");
            F("MoveObjects", () => typeof(WgoBuildPointer), "canTakeResources");
            F("MoveObjects", () => typeof(WgoBuildPointer), "takeResourcesAction");
            F("MoveObjects", () => typeof(WgoBuildPointer), "buildColliders");
            F("MoveObjects", () => typeof(WgoBuildPointer), "shownAsActive");
            F("MoveObjects", () => typeof(WgoData), "isRemovingFromData");
            F("MoveObjects", () => typeof(WgoData), "gdPointsRegistered");
            F("CraftMax", () => typeof(UIBaseCraftSelectionWindow), "plusCraftButton");
            M("Respec", () => typeof(TalentLevelUpWidget), "OnOver");
            M("Respec", () => typeof(TalentLevelUpWidget), "OnOut");
            M("Respec", () => typeof(TechTreePageWidget), "OnTechClicked");
            M("Respec", () => typeof(TechTreePageWidget), "UpdateElements");
            M("CraftMax", () => typeof(UIBaseCraftSelectionWindow), "ChangeCraftCount");
            M("MoveSpeed", () => typeof(PlayerPhysicalBody), "MoveByDirection");
            M("CraftRepair", () => typeof(CraftInteractionHandler), "Interact");
            F("CraftRepair", () => typeof(WGOInteractionHandlerBase), "assignedWgo");
            M("CraftRepair", () => typeof(CraftComponent), "RemoveFromQueue");
            M("CraftRepair", () => typeof(CraftComponent), "GetStartCraftStatus");
            // 1.7.7: Kein Speichern im Kampf (BattleSavePatch) und Arbeit wiederholen
            M("Saves", () => typeof(SaveSystem), "Save");
            // Bewusst KEINE Pruefung von FightingGameController.CurrentFightState: fehlt es, liefert Backups.InBattle() false
            // (try/catch) - dann blockiert BattleSave nur nichts, die Backups duerfen deswegen nicht mit abgeschaltet werden.
            M("RepeatWork", () => typeof(CraftInteractionHandler), "Interact");
            M("RepeatWork", () => typeof(CraftInteractionHandler), "HasInteraction");
            M("RepeatWork", () => typeof(CraftComponent), "GetStartCraftStatus");
            M("TradeLikes", () => typeof(Trading), "OnPlayerItemPress1");
            F("TradeLikes", () => typeof(Trading), "cachedWindowData");
            F("TradeLikes", () => typeof(Trading), "sellInventory");
            F("TradeLikes", () => typeof(UIItemCountWindow), "slider");
            M("TradeLikes", () => typeof(SmartSlider), "SetValue");
            F("TradeLikes", () => typeof(UIVendorWindow), "sellDealInventoryWidget");
            F("TradeLikes", () => typeof(VendorTierData), "townVendorProductInfos");
            F("ZombieRename", () => typeof(UIZombieWorkerWindow), "nameLabel");
            F("ZombieRename", () => typeof(LazyBearTechnology.LazyUI), "windowsCache");
            F("TradeLikes", () => typeof(LazyBearTechnology.LazyUI), "windowsCache");
            M("ZombieRename", () => typeof(UIZombieWorkerWindow), "RedrawName");
            M("ZombieRename", () => typeof(ZombieWgoData), "SetName");
            M("ZombieRename", () => typeof(ZombieWgoData), "RollName");
            M("InstantRemove", () => typeof(CraftElementBase), "SetCustomOutputItems");
            F("InstantRemove", () => typeof(CraftElementBase), "customCraftOutput");
            F("Pins", () => typeof(GameBalance), "craftDefs");
            F("Pins", () => typeof(CraftDefBase), "needItemsFromWgo");
            F("Pins", () => typeof(CraftDefBase), "craftsIn");
            M("Graphics", () => typeof(PlatformFeatureEntry), "Clone");
            M("Zoom", () => typeof(CameraSystem), "CalculateOrthographicSize");
            M("Zoom", () => typeof(CameraSystem), "SetOrthographicSize");
            F("Zoom", () => typeof(ResolutionConfig), "PixelSize");
            F("WeekPlan", () => typeof(UIBaseNotification), "displayingTime");
            F("TradeLikes", () => typeof(UIItemCountWindowData), "IsForVendor");

            // Ab 1.011 verschiebt das Spiel Objekte selbst (BuildingMode.Move). Die alten Bau-Modus-Felder fuer
            // "Objekte verschieben" fehlen dann (z. B. RemovePointer.currentRemovingSelection) - das ist kein Fehler:
            // die Funktion bleibt still aus, ihre Patches entfallen, ohne Hinweis "abgeschaltet".
            bool nativeMove = false;
            try { nativeMove = BuildMove.GameHasNativeMove; } catch { }
            if (nativeMove)
            {
                retired.Add("MoveObjects");
                disabled["MoveObjects"] = "the game moves objects itself";
            }
            // HUD- & Menuegroesse braucht ResolutionConfig.GetUiLayout (ab Spiel 1.011) - vorher still aus, ohne Hinweis
            bool uiLayout = false;
            try { uiLayout = HasMethod(typeof(ResolutionConfig), "GetUiLayout"); } catch { }
            if (!uiLayout)
            {
                retired.Add("UiScale");
                disabled["UiScale"] = "needs game 1.011 or newer";
            }

            foreach (Check c in checks)
            {
                bool ok;
                string what = c.Member;
                try
                {
                    Type t = c.Type();
                    what = t.Name + "." + c.Member;
                    ok = c.Test(t);
                }
                catch (Exception e) { ok = false; what += " (" + e.GetType().Name + ")"; }
                if (!ok) Disable(c.Feature, "missing " + what, false);
            }

            string game = Application.version;
            string last = Plugin.LastGameVersion.Value;
            if (!string.IsNullOrEmpty(last) && last != game)
            {
                NoticePending = true; gameFrom = last; gameTo = game;
                Plugin.Log.LogInfo("Game version changed " + last + " -> " + game + ", disabled: " + string.Join(", ", Disabled));
            }
            Plugin.LastGameVersion.Value = game;
            Plugin.Log.LogInfo("Safe mode check: " + checks.Count + " game members checked, " + disabled.Count + " feature(s) disabled");
        }

        // Hinweis nach einem Spiel-Update (erst beim Anzeigen uebersetzen, dann steht die Sprache fest)
        internal static string UpdateNotice() => ShownCount() == 0
            ? string.Format(Labels.T("Spiel-Update erkannt ({0} → {1}): Vanilla+ hat alles geprüft, alle Funktionen laufen.",
                "Game update detected ({0} → {1}): Vanilla+ checked everything, all features work."), gameFrom, gameTo)
            : string.Format(Labels.T("Spiel-Update erkannt ({0} → {1}): Vanilla+ hat {2} Funktion(en) sicherheitshalber abgeschaltet – Details im Mod-Menü.",
                "Game update detected ({0} → {1}): Vanilla+ turned off {2} feature(s) to be safe – details in the mod menu."), gameFrom, gameTo, ShownCount());

        // Hinweis im Mod-Menue
        internal static string MenuNotice()
        {
            if (ShownCount() == 0) return null;
            var list = new List<string>();
            foreach (string f in disabled.Keys) if (!retired.Contains(f)) list.Add(Name(f));
            return string.Format(Labels.T("Sicherer Modus: {0} abgeschaltet, weil das Spiel sich geändert hat. Der Rest läuft normal – bitte auf ein Mod-Update warten.",
                "Safe mode: {0} turned off because the game changed. Everything else works – please wait for a mod update."), string.Join(", ", list));
        }
    }
}
