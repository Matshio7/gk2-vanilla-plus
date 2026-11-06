using System;
using System.Linq;
using BepInEx.Configuration;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // 1.7.7: Arbeit wiederholen (nicht Vanilla, standardmaessig AUS) - Wunsch DokMo.
    // Taste einmal an einer Werkbank druecken, die der Spieler selbst bedient: Vanilla+ drueckt dann jedes Mal F
    // (CraftInteractionHandler.Interact, genau die Methode, die das F des Spiels erreicht), sobald der naechste
    // Auftrag der Warteschlange starten kann - bis die Warteschlange leer ist, der Spieler weggeht, ein Fenster
    // aufgeht oder ein Kampf beginnt. Kein Harmony-Patch, nur Aufrufe, die das Repo schon nutzt.
    // Unbekannt (kein Spiel-Quelltext hier): ob Interact bei voller Warteschlange arbeitet oder das Herstellfenster
    // oeffnet. Oeffnet es das Fenster, hoert die Funktion sofort mit Hinweis auf und laesst das Fenster offen.
    internal static class RepeatWork
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> Key;

        internal static void BindConfig(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Comfort", "RepeatWork", false,
                "Repeat work (not vanilla): press the Repeat work key once at a workbench you work at yourself; Vanilla+ then presses F for you whenever the next queued craft is ready to start, until the queue is empty, you walk away, a Vanilla+ or craft window opens or a battle starts. It waits while another menu is open or the game is paused. Off by default.");
            Key = cfg.Bind("Comfort", "RepeatWorkKey", KeyboardShortcut.Empty,
                "Key that starts/stops Repeat work at the workbench you stand at (empty = feature unusable).");
        }

        private const float Interval = 1.5f;      // fruehestens so oft ein automatisches F
        private const float EffectDelay = 1f;     // so lange nach einem F wird geprueft, ob es etwas bewirkt hat
        private const float Range = 2.5f;         // Werkbank-Suche um den Spieler
        private const float MoveLimit = 0.75f;    // so viel weiter weg von der Werkbank als beim Einschalten -> aus
        private const float IdleLimit = 180f;     // so lange ohne moegliches F -> aus (Schutz)
        private const int MaxFires = 200;

        private static Wgo wgo;
        private static CraftInteractionHandler h;
        private static float armDist;             // Abstand Spieler-Werkbank beim Einschalten
        private static float next, lastFire = -1f, lastActive, stepAt;
        private static int fires, noEffect, notReachable;
        private static bool armed, checkPending;

        // Zustand direkt vor dem F (fuer die Wirkungspruefung)
        private static bool snapWorker, snapStarted;
        private static CraftComponentStatus snapStatus;
        private static CraftElementBase snapCur;
        private static int snapCount;

        private static bool On => Enabled != null && Enabled.Value && Key != null && Key.Value.MainKey != KeyCode.None;

        internal static void Tick()
        {
            try
            {
                if (!On) { if (armed) Stop(Labels.T("ausgeschaltet", "turned off"), false); return; }
                bool menu = Plugin.Instance != null && Plugin.Instance.Gui != null && Plugin.Instance.Gui.MenuOpen;
                if (!menu && Key.Value.IsDown())
                {
                    if (armed) Stop(null, true);
                    else Arm();
                    return;
                }
                if (armed) Step();
            }
            catch (Exception e)
            {
                SafeMode.Fail("RepeatWork", e);
                if (armed) Stop(Labels.T("Fehler", "error"), false);
            }
        }

        private static void Arm()
        {
            MainGame mg = MainGame.Instance;
            PlayerController pc = MainGame.PlayerController;
            if (mg == null || mg.gameState != MainGame.GameState.InGame || pc == null) return;
            if (Backups.InBattle()) { ManualSave.Toast(Labels.T("Nicht während eines Kampfes.", "Not during a battle."), 3f); return; }
            if (Plugin.RepairWorkbenches.Value)
            {
                // CraftRepairPatch liefe sonst bei jedem automatischen F mit (Pruefung + Hinweis alle 5 s)
                ManualSave.Toast(Labels.T("Arbeit wiederholen geht nicht, solange „Hängende Werkbank reparieren“ an ist.",
                                          "Repeat work is not possible while \"Repair stuck workbench\" is on."), 5f);
                return;
            }
            // Bewusst auch erlaubt, waehrend der Spieler gerade arbeitet (Steuerung belegt): Step wartet dann einfach.
            if (WindowOpen()) return;

            Vector3 pos = pc.transform.position;
            Wgo best = null;
            float bestD = Range;
            foreach (Wgo w in UnityEngine.Object.FindObjectsByType<Wgo>(FindObjectsSortMode.None))
            {
                if (w == null) continue;
                float dist = Vector3.Distance(pos, w.transform.position);
                if (dist > bestD || !Usable(w)) continue;
                best = w; bestD = dist;
            }
            if (best == null)
            {
                ManualSave.Toast(Labels.T("Arbeit wiederholen: stell dich an eine Werkbank mit Aufträgen in der Warteschlange.",
                                          "Repeat work: stand at a workbench with queued crafts."), 4f);
                return;
            }

            wgo = best;
            h = (CraftInteractionHandler)best.InteractionHandler;
            armDist = bestD;
            float now = Time.realtimeSinceStartup;
            next = now; lastActive = now; lastFire = -1f; stepAt = 0f;
            fires = 0; noEffect = 0; notReachable = 0;
            checkPending = false;
            armed = true;
            ManualSave.Toast(Labels.T("Arbeit wiederholen an – weggehen oder Taste erneut drücken zum Beenden.",
                                      "Repeat work on – walk away or press the key again to stop."), 4f);
            Plugin.Log.LogInfo("Repeat work: armed at " + CraftRepair.Describe(best.Data, best.Data.CraftComponent) + " d=" + bestD.ToString("0.00")
                + (pc.IsControlsEnabledExcept() ? "" : " (player busy, waiting)"));
        }

        // nur Werkbaenke, die der Spieler selbst bedient (kein Zombie, keine Fabrik), mit Auftraegen
        private static bool Usable(Wgo w)
        {
            WgoData d = w.Data;
            if (d == null || !(w.InteractionHandler is CraftInteractionHandler)) return false;
            if (d.CraftableType != CraftableType.Regular || d.CraftableAttachedWorker is ZombieWgoData) return false;
            CraftComponent cc = d.CraftComponent;
            return cc != null && !cc.IsDestroyingCraftActive && cc.CraftElementsQueue != null && cc.CraftElementsQueue.Count > 0;
        }

        private static bool WindowOpen()
        {
            if (Plugin.Instance != null && Plugin.Instance.Gui != null && Plugin.Instance.Gui.AnyWindowOpen) return true;
            if (CraftMax.Open() != null) return true;
            try { if (LazyUI.GetWindow<UICraftWindow>().IsShown) return true; } catch { }
            return false;
        }

        private static void Step()
        {
            float now = Time.realtimeSinceStartup;
            if (!SafeMode.On("RepeatWork")) { Stop(Labels.T("Fehler", "error"), false); return; }
            // CraftRepairPatch liefe sonst bei jedem automatischen F mit (Option waehrend des Laufs eingeschaltet)
            if (Plugin.RepairWorkbenches.Value) { Stop(Labels.T("Fehler", "error"), false); return; }
            if (now < stepAt) return;
            stepAt = now + 0.2f;
            // Spiel pausiert (Pause, Hintergrund): nichts druecken, Zeitgrenzen anhalten
            if (Time.timeScale <= 0f)
            {
                next = now + 0.5f;
                lastActive = now;
                if (checkPending) lastFire = now;
                return;
            }
            if (Backups.InBattleCached()) { Stop(Labels.T("Kampf", "battle"), false); return; }

            if (WindowOpen())
            {
                // direkt nach unserem F aufgegangen: hier oeffnet F das Fenster - Fenster bleibt offen wie beim echten F
                if (lastFire >= 0f && now - lastFire < EffectDelay + 0.5f)
                {
                    Plugin.Log.LogInfo("Repeat work: the game opened a window after F - not supported at this workbench");
                    Stop(null, false);
                    ManualSave.Toast(Labels.T("Arbeit wiederholen: hier öffnet F das Herstellfenster – an dieser Werkbank nicht möglich.",
                                              "Repeat work: here F opens the craft window – not supported at this workbench."), 6f);
                }
                else Stop(Labels.T("ein Fenster ist offen", "a window opened"), false);
                return;
            }

            PlayerController pc = MainGame.PlayerController;
            WgoData d = wgo != null ? wgo.Data : null;
            CraftComponent cc = d != null ? d.CraftComponent : null;
            if (pc == null || d == null || cc == null || h == null) { Stop(Labels.T("Werkbank weg", "workbench gone"), false); return; }
            // Abstand zur Werkbank statt zur Startposition: F stellt die Figur evtl. an den Arbeitsplatz der Werkbank
            if (Vector3.Distance(pc.transform.position, wgo.transform.position) > Mathf.Max(armDist, Range) + MoveLimit)
            { Stop(Labels.T("du bist weggegangen", "you moved"), false); return; }
            if (cc.CraftElementsQueue == null || cc.CraftElementsQueue.Count == 0) { Stop(Labels.T("Warteschlange fertig", "queue finished"), false); return; }

            CraftElementBase cur = cc.CurrentCraftElement;
            bool running = cur != null && cur.IsStarted;
            bool anyStarted = running || cc.CraftElementsQueue.Any(x => x != null && x.IsStarted);
            CraftElementBase front = cc.CraftElementsQueue.FirstOrDefault(x => x != null && !x.IsStarted);
            CraftStatus fs = front != null ? cc.GetStartCraftStatus(front) : CraftStatus.OK;

            if (!anyStarted)
            {
                if (front == null) { Stop(Labels.T("Warteschlange fertig", "queue finished"), false); return; }
                if (fs != CraftStatus.OK && !cc.CraftElementsQueue.Any(x => x != null && !x.IsStarted && cc.GetStartCraftStatus(x) == CraftStatus.OK))
                {
                    Stop(CraftRepair.Reason(fs), false);
                    return;
                }
            }

            // Wirkung des letzten F pruefen
            if (checkPending && now - lastFire >= EffectDelay)
            {
                checkPending = false;
                bool changed = (d.Worker != null) != snapWorker || cc.Status != snapStatus || cur != snapCur
                    || running != snapStarted || cc.CraftElementsQueue.Count != snapCount;
                if (changed) { noEffect = 0; lastActive = now; }
                else
                {
                    noEffect++;
                    Plugin.Log.LogInfo("Repeat work: F #" + fires + " had no visible effect (" + noEffect + ")");
                    if (noEffect >= 2) { Stop(Labels.T("keine Wirkung", "nothing happened"), false); return; }
                }
            }

            if (fires >= MaxFires) { Stop(Labels.T("Höchstzahl erreicht", "limit reached"), false); return; }

            bool waiting = cc.Status == CraftComponentStatus.QueueDelayed || cc.Status == CraftComponentStatus.ReadyToStartCraft;
            bool canFire = !running && d.Worker == null && waiting && front != null && fs == CraftStatus.OK;
            if (!canFire)
            {
                // Spieler arbeitet / Durchgang laeuft: nur warten
                if (running || d.Worker != null) lastActive = now;
                else if (now - lastActive > IdleLimit) Stop(Labels.T("keine Wirkung", "nothing happened"), false);
                return;
            }
            if (now < next || checkPending) return;
            if (!pc.IsControlsEnabledExcept()) { next = now + 0.5f; return; }

            if (!h.HasInteraction(pc))
            {
                notReachable++;
                next = now + Interval;
                Plugin.Log.LogInfo("Repeat work: HasInteraction=false (" + notReachable + ")");
                if (notReachable >= 3) Stop(Labels.T("nicht erreichbar", "not reachable"), false);
                return;
            }
            notReachable = 0;

            snapWorker = d.Worker != null;
            snapStatus = cc.Status;
            snapCur = cur;
            snapStarted = running;
            snapCount = cc.CraftElementsQueue.Count;

            bool r = h.Interact(pc);
            fires++;
            lastFire = now;
            next = now + Interval;
            checkPending = true;
            lastActive = now;
            CraftElementBase after = cc.CurrentCraftElement;
            Plugin.Log.LogInfo("Repeat work: F #" + fires + " Interact=" + r + " status=" + cc.Status
                + " worker=" + (d.Worker == null ? "-" : d.Worker.GetType().Name)
                + " current=" + (after == null ? "-" : after.CraftId + (after.IsStarted ? " started" : " waiting")));
        }

        // reason == null + byKey: Taste; reason == null sonst: eigener Hinweis kommt vom Aufrufer
        private static void Stop(string reason, bool byKey)
        {
            bool was = armed;
            armed = false;
            checkPending = false;
            wgo = null;
            h = null;
            snapCur = null;
            if (!was) return;
            Plugin.Log.LogInfo("Repeat work: stopped (" + (byKey ? "key" : reason ?? "window after F") + ") after " + fires + " presses");
            if (byKey) ManualSave.Toast(Labels.T("Arbeit wiederholen aus.", "Repeat work off."), 3f);
            else if (reason != null) ManualSave.Toast(string.Format(Labels.T("Arbeit wiederholen aus ({0}).", "Repeat work off ({0})."), reason), 4f);
        }
    }
}
