using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    // 1.7.7: Kein Speichern im Kampf (Wunsch Diepie2635). Autosaves mitten im Kampf sind oft kaputt.
    // Das Repo kann den Autosave des Spiels nicht von anderen Speichervorgaengen unterscheiden (kein Aufrufer-Flag),
    // darum wird waehrend eines Kampfs JEDES SaveSystem.Save uebersprungen und der Aufrufer geloggt.
    // Nach dem Kampf speichert Vanilla+ genau einmal, sobald der Spieler wieder frei steuerbar ist -
    // ausser das Spiel hat das inzwischen selbst getan.
    internal static class BattleSave
    {
        internal static ConfigEntry<bool> Enabled;

        internal static void BindConfig(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Comfort", "NoBattleAutosave", false,
                "No saving during a battle: saves the game makes while a battle is running (often broken) are skipped, and Vanilla+ saves once right after the battle instead. If you quit during a battle, the save from before the battle is kept.");
        }

        private const float QuietSeconds = 2f;       // so lange frei steuerbar nach dem Kampf, bevor gespeichert wird
        private const float MaxBlockMinutes = 10f;   // Schutz, falls InBattle() haengen bleibt
        private const float SaveAnswerSeconds = 30f; // keine Rueckmeldung vom Speichern -> weiter

        private static int skipped;
        private static bool pending;
        private static float firstSkip = -1f;
        private static float freeSince = -1f;
        private static bool saving;
        private static float savingSince = -1f;
        private static bool timeoutLogged;
        private static int cacheFrame = -1;
        private static bool cacheBlock;
        private static object pendingSave;           // GameSave, fuer den das Speichern nach dem Kampf aussteht
        private static float stateLogAt = -1f;

        private static bool On => Enabled != null && Enabled.Value;

        // Von BackupPatch und BattleSavePatch fuer denselben Save-Aufruf gefragt - je Frame zwischengespeichert,
        // damit beide garantiert dieselbe Antwort bekommen.
        internal static bool ShouldBlock()
        {
            if (Time.frameCount == cacheFrame) return cacheBlock;
            cacheFrame = Time.frameCount;
            cacheBlock = On && !Plugin.BenchOn && SafeMode.On("Saves") && !saving && Backups.InBattle() && !TimedOut();
            return cacheBlock;
        }

        // Fuer die eigenen Speicher-Funktionen von Vanilla+ (Autosave-Timer, Speichern-Taste): lieber gar nicht erst versuchen
        // Der Schutz-Timer startet auch hier, falls das Spiel selbst nie speichern will (sonst bliebe der Autosave-Timer
        // von Vanilla+ bei haengendem InBattle() fuer immer aus).
        internal static bool BlocksManual()
        {
            bool b = On && !Plugin.BenchOn && SafeMode.On("Saves") && Backups.InBattle();
            if (b && firstSkip < 0f) firstSkip = Time.realtimeSinceStartup;
            return b && !TimedOut();
        }

        private static bool TimedOut()
        {
            if (firstSkip < 0f) return false;
            if (Time.realtimeSinceStartup - firstSkip <= MaxBlockMinutes * 60f) return false;
            if (!timeoutLogged)
            {
                timeoutLogged = true;
                Plugin.Log.LogWarning("Battle: still in battle after " + MaxBlockMinutes + " min (state " + Backups.FightStateText() + ") - saves allowed again");
            }
            return true;
        }

        // Ein Speichern im Kampf wurde uebersprungen. Der Erfolgs-Callback wird trotzdem aufgerufen (wie SaveBlockPatch im
        // Messmodus): ein Aufrufer, der darauf wartet (Speichern & Beenden, Szenenwechsel), darf nie haengen bleiben.
        // Auch Speichern & Beenden wird uebersprungen: dann bleibt der Stand von vor dem Kampf (steht im Tooltip).
        internal static void Skip(Action callbackSuccessful)
        {
            skipped++;
            pending = true;
            if (firstSkip < 0f) firstSkip = Time.realtimeSinceStartup;
            try { pendingSave = MainGame.Instance != null ? MainGame.Instance.GameSave : null; } catch { pendingSave = null; }
            Plugin.Log.LogInfo("Battle: autosave skipped (" + skipped + " this battle, caller " + Caller() + ", state " + Backups.FightStateText() + ")");
            if (skipped == 1)
                ManualSave.Toast(Labels.T("Im Kampf nicht gespeichert – Vanilla+ speichert direkt nach dem Kampf.",
                                          "Not saved during the battle – Vanilla+ saves right after it."), 4f);
            try { callbackSuccessful?.Invoke(); }
            catch (Exception e) { Plugin.Log.LogWarning("Battle: save callback failed: " + e.Message); }
        }

        // Speichern-Taste / "Jetzt speichern" im Kampf abgelehnt: nach dem Kampf trotzdem einmal speichern,
        // wie im Hinweis versprochen (auch wenn das Spiel selbst im Kampf nichts speichern wollte)
        internal static void Request()
        {
            if (!pending) Plugin.Log.LogInfo("Battle: save requested during the battle - saving right after it");
            pending = true;
            if (firstSkip < 0f) firstSkip = Time.realtimeSinceStartup;
            try { pendingSave = MainGame.Instance != null ? MainGame.Instance.GameSave : null; } catch { pendingSave = null; }
        }

        // Ein Speichern ging durch: hat das Spiel nach dem Kampf schon selbst gespeichert, braucht es unseres nicht mehr.
        internal static void Seen()
        {
            if (!pending || saving || Backups.InBattle()) return;
            Plugin.Log.LogInfo("Battle: game saved after the battle itself - no extra save needed");
            Reset();
        }

        // Erster Aufrufer ausserhalb von Vanilla+, SaveSystem und Harmony-Huellen (fuer das Log)
        private static string Caller()
        {
            try
            {
                var st = new System.Diagnostics.StackTrace(false);
                for (int i = 0; i < st.FrameCount; i++)
                {
                    System.Reflection.MethodBase m = st.GetFrame(i)?.GetMethod();
                    Type t = m?.DeclaringType;
                    if (t == null) continue;
                    if (t.Namespace == "GK2Tweaks" || t == typeof(SaveSystem)) continue;
                    if (t.Name.StartsWith("DMD") || t.Name.StartsWith("Harmony")) continue;
                    if (m.Name.StartsWith("DMD")) continue;
                    return t.Name + "." + m.Name;
                }
            }
            catch { }
            return "?";
        }

        private static void Reset()
        {
            pending = false;
            skipped = 0;
            firstSkip = -1f;
            freeSince = -1f;
            timeoutLogged = false;
            pendingSave = null;
            stateLogAt = -1f;
        }

        private static void SaveDone()
        {
            saving = false;
            savingSince = -1f;
            cacheFrame = -1;
        }

        // Aus Plugin.Update: SafeMode.Run("Saves", BattleSave.Tick). Eigene Fehler gehen nur ins Log (Report statt Fail),
        // damit BattleSave nie die Backups ("Saves") mit abschalten kann.
        internal static void Tick()
        {
            try { TickInner(); }
            catch (Exception e)
            {
                SafeMode.Report("BattleSave", e);
                Reset();
            }
        }

        private static void TickInner()
        {
            float now = Time.realtimeSinceStartup;
            if (saving && savingSince >= 0f && now - savingSince > SaveAnswerSeconds)
            {
                Plugin.Log.LogWarning("Battle: no answer from the save, continuing");
                SaveDone();
            }
            if (!pending)
            {
                freeSince = -1f;
                // Schutz-Timer aus BlocksManual() erst nach dem Kampf zuruecksetzen
                if (firstSkip >= 0f && !Backups.InBattleCached()) { firstSkip = -1f; timeoutLogged = false; }
                return;
            }
            MainGame mg = MainGame.Instance;
            if (mg == null || mg.gameState != MainGame.GameState.InGame || mg.GameSave == null)
            {
                // Hauptmenue, Laden, Beenden: nichts mehr zu speichern, auf der Platte bleibt der Stand vor dem Kampf
                Plugin.Log.LogInfo("Battle: pending save dropped (left the game)");
                Reset();
                return;
            }
            if (pendingSave != null && !ReferenceEquals(mg.GameSave, pendingSave))
            {
                Plugin.Log.LogInfo("Battle: pending save dropped (other save loaded)");
                Reset();
                return;
            }
            // Option mitten im Kampf ausgeschaltet: der naechste Autosave des Spiels deckt es ab
            if (Plugin.BenchOn || !On) { Reset(); return; }
            PlayerController pc = MainGame.PlayerController;
            if (Backups.InBattleCached())
            {
                freeSince = -1f;
                // Spieler frei steuerbar, aber angeblich noch im Kampf: Zustand ins Log (hoechstens alle 30 s)
                if (pc != null && pc.IsControlsEnabledExcept() && (stateLogAt < 0f || now >= stateLogAt))
                {
                    stateLogAt = now + 30f;
                    Plugin.Log.LogInfo("Battle: save pending, controls free, fight state " + Backups.FightStateText());
                }
                return;
            }
            // Dialog, Belohnungsfenster, Zwischensequenz, Ueberblendung, Szenenwechsel: warten
            if (pc == null || !pc.IsControlsEnabledExcept()) { freeSince = -1f; return; }
            if (freeSince < 0f) { freeSince = now; return; }
            if (now - freeSince < QuietSeconds || saving) return;
            if (mg.SaveSlotData == null) { Plugin.Log.LogInfo("Battle: pending save dropped (no save slot)"); Reset(); return; }

            int n = skipped;
            saving = true;
            savingSince = now;
            cacheFrame = -1;
            Reset();
            Backups.ForceNextBackup = true;
            Plugin.Log.LogInfo("Battle: battle over, saving now (" + n + " skipped)");
            try
            {
                SaveSystem.Save(mg.SaveSlotData, mg.GameSave,
                    () =>
                    {
                        SaveDone();
                        Plugin.Log.LogInfo("Battle: saved after the battle");
                        ManualSave.Toast(Labels.T("Nach dem Kampf gespeichert", "Saved after the battle"), 3f);
                    },
                    () =>
                    {
                        SaveDone();
                        Plugin.Log.LogWarning("Battle: save after the battle failed");
                        ManualSave.Toast(Labels.T("Speichern nach dem Kampf fehlgeschlagen.", "Saving after the battle failed."), 4f);
                    });
            }
            catch (Exception e)
            {
                SaveDone();
                Backups.ForceNextBackup = false;
                Plugin.Log.LogWarning("Battle: save after the battle failed: " + e.Message);
                ManualSave.Toast(Labels.T("Speichern nach dem Kampf fehlgeschlagen.", "Saving after the battle failed."), 4f);
            }
        }
    }

    // Laeuft nach BackupPatch (Priority.First). Jeder Fehler -> das Spiel speichert ganz normal.
    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.Save))]
    internal static class BattleSavePatch
    {
        private static bool Prefix(Action callbackSuccessful)
        {
            try
            {
                if (BattleSave.ShouldBlock()) { BattleSave.Skip(callbackSuccessful); return false; }
                BattleSave.Seen();
            }
            catch (Exception e) { SafeMode.Report("BattleSave", e); }
            return true;
        }
    }
}
