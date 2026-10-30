using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GK2Tweaks
{
    // Sicherer Start (Crash-Waechter): Ein Spiel-Update kann den Mod so treffen, dass das Spiel nie im Hauptmenue
    // ankommt (z. B. Schwarzbild nach den Logos). Der Sicherheitsmodus (SafeMode) hilft dann nicht immer, weil der
    // Fehler mitten in der Initialisierung des Spiels steckt. Deshalb:
    //  1. Nach ArmSeconds (8 s) Echtzeit wird eine Marker-Datei BepInEx/GK2VanillaPlus/startup.pending geschrieben -
    //     bewusst nicht sofort in Plugin.Awake: ein Beenden waehrend der Logos oder ein Neustart des Spiels durch Steam
    //     (Start ueber die exe) hinterlaesst so keinen Marker. Das Schwarzbild kommt erst nach den Logos und wird weiter erkannt.
    //     Der Marker wird geloescht, sobald das Hauptmenue wirklich sichtbar ist (Zustand MainMenu seit 3 s UND das
    //     Hauptmenue-Fenster wird angezeigt) oder das Spiel laeuft. Ist der Spielzustand nicht lesbar, nach 120 s ohne Exception.
    //  2. Liegt der Marker beim naechsten Start noch da, kam das Spiel beim letzten Mal nicht bis ins Hauptmenue:
    //     dieser eine Start laeuft "vanilla" - keine Harmony-Patches, keine Funktionen, nur Mod-Menue (F9) und Log.
    //     Auch GK2Ultrawide liest das (AppDomainKey) und patcht dann nichts. Der Marker wird erneut geschrieben,
    //     damit auch dieser Start ueberwacht ist.
    //  3. Kam selbst der Vanilla-Start nicht ins Hauptmenue, liegt es nicht an Vanilla+: Marker loeschen und normal
    //     starten (nie dauerhaft aus bleiben).
    //  4. Linke Umschalttaste beim Spielstart gedrueckt halten erzwingt den Vanilla-Start ebenfalls (einmalig) - nur, wenn
    //     das Spielfenster den Fokus hat (sonst zaehlt Umschalt in einem anderen Fenster nicht).
    //  5. Zusaetzlich: kommt waehrend des Starts eine Exception aus Vanilla+-Code durch eine gepatchte Spielmethode
    //     (Postfix/Prefix im Stacktrace), bleibt der Marker auch dann liegen, wenn das Hauptmenue erreicht wird -
    //     aber nur EINMAL pro Mod- und Spielversion (startup.exception), sonst wechselten sich normale und Vanilla-Starts
    //     endlos ab, wenn die Exception bei jedem Start kommt.
    // Bekannte Grenzen: Haengt das Spiel schon vor ArmSeconds komplett (kein Frame mehr), wird kein Marker geschrieben.
    // Exceptions, die geloggt werden, bevor GK2Tweaks.Awake den Log-Hook setzt, sieht Regel 5 nicht (GK2Ultrawide laedt
    // deshalb nach GK2Tweaks, siehe BepInDependency dort); Regel 1 greift fuer diese Faelle trotzdem.
    internal static class SafeStart
    {
        // AppDomain-weit sichtbar (fuer GK2Ultrawide): "1" = dieser Start laeuft ohne Vanilla+
        internal const string AppDomainKey = "mats.gk2.tweaks.safestart";
        private const string ModeNormal = "normal";
        private const string ModeVanilla = "vanilla";
        private const float ArmSeconds = 8f;
        private const float MenuStableSeconds = 3f;
        private const float FallbackSeconds = 120f;
        private const float NoWindowSeconds = 60f;

        internal static bool Active;            // dieser Start laeuft vanilla (alles aus ausser Mod-Menue und Log)
        internal static bool Forced;            // per linker Umschalttaste erzwungen
        private static bool watching, hooked, toastShown, stateBroken, tickFailed, windowBroken, windowEverSeen, windowShown;
        private static string pendingMode;      // Marker, der nach ArmSeconds geschrieben wird (null = keiner mehr)
        private static volatile bool exceptionSeen, ourExceptionSeen;
        private static string firstOurException;
        private static float startedAt, menuSince = -1f, nextWindowPoll;

        private static string Dir => Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus");
        internal static string MarkerPath => Path.Combine(Dir, "startup.pending");
        private static string ExceptionPath => Path.Combine(Dir, "startup.exception");
        private static string ExceptionKey => "mod " + Plugin.DisplayVersion + " / game " + Application.version;

        // Aus Plugin.Awake, direkt nach BindConfig und vor CheckGame/Patches
        internal static void Begin()
        {
            startedAt = Time.realtimeSinceStartup;
            bool guard = true;
            try { guard = Plugin.SafeStartGuard == null || Plugin.SafeStartGuard.Value; } catch { }
            string previous = ReadMarker();   // null = kein Marker
            if (ShiftHeld())
            {
                Active = true; Forced = true;
                Plugin.Log.LogWarning("Safe start: Left Shift held at startup - Vanilla+ stays fully off for this start.");
            }

            if (!guard)
            {
                if (previous != null) DeleteMarker();
                Plugin.Log.LogInfo("Safe start guard is off (Interface/SafeStartGuard = false), no startup marker written.");
                if (Active) Enter();
                return;
            }

            if (!Active && previous != null)
            {
                if (previous == ModeVanilla)
                {
                    // Auch der Vanilla-Start kam nicht ins Hauptmenue -> das Problem ist nicht Vanilla+
                    Plugin.Log.LogWarning("Safe start: the game did not reach the main menu last time even with Vanilla+ fully off - " +
                        "the problem is not caused by Vanilla+. Marker cleared, Vanilla+ runs normally (this start is not guarded).");
                    DeleteMarker();
                    return;
                }
                Active = true;
                Plugin.Log.LogError("==================== SAFE START ====================");
                Plugin.Log.LogError("Safe start: the game did NOT reach the main menu last time with Vanilla+ active (marker " + MarkerPath + ": " + previous + ").");
                Plugin.Log.LogError("Safe start: Vanilla+ is fully OFF for this start - no patches, no features, only the mod menu (F9) and logging.");
                Plugin.Log.LogError("Safe start: if the game works now, just restart it - Vanilla+ then runs normally again. Please send this log to the author.");
                Plugin.Log.LogError("====================================================");
            }

            if (Active) Enter();
            // Der Marker selbst kommt erst nach ArmSeconds (TickCore). Ein noch liegender alter Marker bleibt bis dahin liegen:
            // endet auch dieser Start frueh, gilt beim naechsten Start dasselbe wie jetzt.
            pendingMode = Active ? ModeVanilla : ModeNormal;
            StartWatching();
        }

        private static void Enter()
        {
            SafeMode.EnterVanillaStart();
            try { AppDomain.CurrentDomain.SetData(AppDomainKey, "1"); } catch { }
        }

        // Aus Plugin.Update (ganz oben, jedes Frame)
        internal static void Tick()
        {
            if (!watching && (!Active || toastShown)) return;
            try { TickCore(); }
            catch (Exception e)
            {
                if (!tickFailed) { tickFailed = true; Plugin.Log.LogWarning("Safe start tick: " + e); }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TickCore()
        {
            float now = Time.realtimeSinceStartup;
            int state = 0;
            if (!stateBroken)
            {
                try { state = GameState(); }
                catch (Exception e) { stateBroken = true; Plugin.Log.LogWarning("Safe start: cannot read the game state (" + e.GetType().Name + ": " + e.Message + "), using the 120 s fallback."); }
            }
            if (state == 0) menuSince = -1f;
            else if (menuSince < 0f) menuSince = now;
            // Im Spiel = sicher erreicht. Hauptmenue: Zustand seit 3 s UND Fenster sichtbar (Zustand allein kam auch beim Schwarzbild).
            bool reached = state == 2 || (state == 1 && now - menuSince >= MenuStableSeconds && MenuShown(now));

            if (watching)
            {
                if (reached)
                {
                    if (ourExceptionSeen && !Active)
                    {
                        if (ExceptionRuleUsed())
                        {
                            Plugin.Log.LogWarning("Safe start: an exception from Vanilla+ code came out of a game method during startup again (" + firstOurException +
                                "). The safe start for this was already used once for " + ExceptionKey + " - not repeated. Please send the log to the author.");
                            Resolve(state == 2 ? "game started" : "main menu reached");
                        }
                        else KeepMarker();
                    }
                    else Resolve(state == 2 ? "game started" : "main menu reached");
                }
                else
                {
                    if (pendingMode != null && now - startedAt >= ArmSeconds)
                    {
                        string mode = pendingMode;
                        pendingMode = null;
                        if (!WriteMarker(mode)) { watching = false; Unhook(); }
                    }
                    // Zeitgrenze nur, wenn der Zustand nicht lesbar ist. Sonst ohne Zeitgrenze weiter warten: beendet der
                    // Spieler ein haengendes Spiel, bleibt der Marker - genau dafuer ist er da.
                    if (watching && stateBroken && now - startedAt >= FallbackSeconds)
                    {
                        if (!exceptionSeen) Resolve(FallbackSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " s without exceptions (game state unreadable)");
                        else
                        {
                            EnsureMarker();
                            watching = false; Unhook();
                            Plugin.Log.LogWarning("Safe start: game state unreadable and exceptions were logged during startup - the startup marker stays.");
                        }
                    }
                }
            }

            if (Active && !toastShown && (reached || now - startedAt >= FallbackSeconds))
            {
                toastShown = true;
                ManualSave.Toast(Notice(), 12f);
            }
        }

        // 0 = noch nicht im Menue, 1 = Hauptmenue, 2 = im Spiel. Eigene Methode: fehlt MainGame nach einem Update,
        // wirft erst dieser Aufruf (beim JIT), nicht TickCore.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GameState()
        {
            MainGame mg = MainGame.Instance;
            if (mg == null) return 0;
            if (mg.gameState == MainGame.GameState.MainMenu) return 1;
            if (mg.gameState == MainGame.GameState.InGame) return 2;
            return 0;
        }

        // Ist das Hauptmenue-Fenster sichtbar? Alle 0,5 s geprueft. Laesst sich das nicht pruefen (Typ fehlt nach einem
        // Update, oder nach NoWindowSeconds im Hauptmenue-Zustand nie ein Fenster gefunden), entscheidet der Zustand allein -
        // sonst bliebe der Marker bei jedem Start liegen.
        private static bool MenuShown(float now)
        {
            if (windowBroken) return true;
            if (now >= nextWindowPoll)
            {
                nextWindowPoll = now + 0.5f;
                int w;
                try { w = MainMenuWindowState(); }
                catch (Exception e)
                {
                    windowBroken = true;
                    Plugin.Log.LogWarning("Safe start: cannot check the main menu window (" + e.GetType().Name + ": " + e.Message + "), using the game state only.");
                    return true;
                }
                if (w >= 0) windowEverSeen = true;
                windowShown = w == 1;
            }
            if (windowShown) return true;
            if (!windowEverSeen && menuSince >= 0f && now - menuSince >= NoWindowSeconds)
            {
                windowBroken = true;
                Plugin.Log.LogWarning("Safe start: main menu state for " + NoWindowSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                    " s but no main menu window found - using the game state only.");
                return true;
            }
            return false;
        }

        // -1 = kein Hauptmenue-Fenster, 0 = vorhanden, aber nicht sichtbar, 1 = sichtbar (wie CraftMax/Benchmark)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MainMenuWindowState()
        {
            UIMainMenuWindow w = UnityEngine.Object.FindFirstObjectByType<UIMainMenuWindow>(FindObjectsInactive.Include);
            if (w == null) return -1;
            return w.IsShown && ((Component)w).gameObject.activeInHierarchy ? 1 : 0;
        }

        private static void Resolve(string why)
        {
            watching = false;
            pendingMode = null;
            Unhook();
            DeleteMarker();
            Plugin.Log.LogInfo("Safe start: " + why + " - startup marker cleared" +
                (Active ? ". Safe start worked; restart the game to run Vanilla+ normally again." : "."));
        }

        // Regel 5: Marker bleibt (wird jetzt geschrieben, falls ArmSeconds noch nicht um war), einmal pro Mod-/Spielversion
        private static void KeepMarker()
        {
            EnsureMarker();
            watching = false; Unhook();
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(ExceptionPath, ExceptionKey + "\n");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Safe start: cannot write " + ExceptionPath + ": " + e.Message); }
            Plugin.Log.LogError("Safe start: main menu reached, but an exception from Vanilla+ code came out of a game method during startup: " +
                firstOurException + " - the startup marker stays, the next start runs without Vanilla+ once (only once for " + ExceptionKey + ").");
        }

        private static void EnsureMarker()
        {
            if (pendingMode == null) return;
            string mode = pendingMode;
            pendingMode = null;
            WriteMarker(mode);
        }

        private static bool ExceptionRuleUsed()
        {
            try
            {
                string path = ExceptionPath;
                return File.Exists(path) && File.ReadAllText(path).Trim() == ExceptionKey;
            }
            catch { return false; }
        }

        // ---------- Texte ----------
        internal static string Notice() => Forced
            ? Labels.T("Sicherer Start (linke Umschalttaste gehalten): Vanilla+ ist einmal komplett aus. Zum Wiedereinschalten einfach neu starten.",
                "Safe start (Left Shift held): Vanilla+ is fully off this once. Just restart to turn it back on.")
            : Labels.T("Sicherer Start: Beim letzten Start kam das Spiel nicht bis ins Hauptmenü – Vanilla+ ist deshalb einmal komplett aus. Funktioniert alles, einfach neu starten.",
                "Safe start: last time the game did not reach the main menu – Vanilla+ is fully off this once. If all is well, just restart.");

        // Hinweis oben im Mod-Menue (null = kein Hinweis). Im Vanilla-Start laufen keine Funktionen: Einstellungen wirken erst nach einem Neustart.
        internal static string MenuNotice() => Active
            ? Notice() + "\n" + Labels.T("Änderungen an den Einstellungen wirken erst nach einem Neustart des Spiels.",
                "Changes to the settings take effect only after restarting the game.")
            : null;

        // ---------- Exceptions waehrend des Starts beobachten ----------
        private static void StartWatching()
        {
            watching = true;
            try { Application.logMessageReceived += OnLog; hooked = true; }
            catch (Exception e) { Plugin.Log.LogWarning("Safe start: log hook failed: " + e.Message); }
        }

        private static void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            try { Application.logMessageReceived -= OnLog; } catch { }
        }

        // Nur Flags setzen - hier nichts loggen (wuerde wieder im Unity-Log landen)
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return;
            exceptionSeen = true;
            try
            {
                string s = (condition ?? "") + "\n" + (stackTrace ?? "");
                bool ours = s.IndexOf("GK2Tweaks.", StringComparison.Ordinal) >= 0 || s.IndexOf("GK2Ultrawide.", StringComparison.Ordinal) >= 0;
                bool viaPatch = s.IndexOf("DMD<", StringComparison.Ordinal) >= 0 || s.IndexOf(".Postfix", StringComparison.Ordinal) >= 0
                    || s.IndexOf(".Prefix", StringComparison.Ordinal) >= 0 || s.IndexOf(".Transpiler", StringComparison.Ordinal) >= 0;
                if (ours && viaPatch && !ourExceptionSeen) { firstOurException = condition; ourExceptionSeen = true; }
            }
            catch { }
        }

        // ---------- Marker-Datei ----------
        private static string ReadMarker()
        {
            try
            {
                string path = MarkerPath;
                if (!File.Exists(path)) return null;
                string[] lines = File.ReadAllLines(path);
                string mode = lines.Length > 0 ? lines[0].Trim() : "";
                return mode == ModeVanilla ? ModeVanilla : (mode.Length > 0 ? mode : ModeNormal);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Safe start: cannot read marker: " + e.Message); return null; }
        }

        private static bool WriteMarker(string mode)
        {
            try
            {
                string path = MarkerPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, mode + "\nmod " + Plugin.DisplayVersion + "\ngame " + Application.version + "\nstarted " +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\n");
                return true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Safe start: cannot write marker (" + e.Message + "), this start is not guarded."); return false; }
        }

        private static void DeleteMarker()
        {
            try { string path = MarkerPath; if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Plugin.Log.LogWarning("Safe start: cannot delete marker: " + e.Message); }
        }

        // ---------- Notschalter: linke Umschalttaste ----------
        // Input.GetKey liefert so frueh im Start (noch vor dem ersten Frame) oft nichts, deshalb unter Windows
        // (auch Proton/Wine) zusaetzlich direkt den Tastenzustand abfragen. GetAsyncKeyState gilt systemweit -
        // deshalb nur, wenn das Vordergrundfenster zu diesem Prozess gehoert (Umschalt in Discord/Browser zaehlt nicht).
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        private const int VK_LSHIFT = 0xA0;

        private static bool ShiftHeld()
        {
            try { if (Input.GetKey(KeyCode.LeftShift)) return true; } catch { }
            try
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer && (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0)
                {
                    if (GameWindowFocused()) return true;
                    Plugin.Log.LogInfo("Safe start: Left Shift is held, but the game window is not in front - ignored.");
                }
            }
            catch { }
            return false;
        }

        private static bool GameWindowFocused()
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                if (h == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                return pid != 0 && pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch { return false; }
        }
    }
}
