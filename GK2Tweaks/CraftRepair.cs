using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Haengende Werkbaenke loesen (Option "Haengende Werkbaenke loesen", standardmaessig AUS). Laeuft, wenn der Spieler
    // an einer Werkbank F drueckt oder sie abbauen will. Nur Werkbaenke, die der Spieler selbst bedient (kein Zombie,
    // keine Fabrik). Jede Pruefung schreibt eine Zeile mit dem kompletten Zustand ins Log und sagt kurz, was los war.
    //
    // 1.7.2: Wartet vorne ein Auftrag, fuer den Zutaten / Werkzeug fehlen, wird nur DIESER herausgenommen
    //        (Zutaten werden erst beim Start eines Durchgangs verbraucht - es geht nichts verloren).
    // 1.7.4 (piermaz38, Drizz - Werkbank reagiert nicht, kein Fortschrittsbalken, Rezepte lassen sich aber anlegen):
    //  - Werkbank steht auf "laeuft" (oder fertig/abgebrochen), obwohl der aktuelle Auftrag gar nicht gestartet ist.
    //    Dann startet das Spiel nie wieder etwas (UpdateManual wartet auf "Warteschlange verzoegert") - auch nach
    //    Neustart, der Zustand steckt im Spielstand. Loesung: auf "Warteschlange verzoegert" setzen und die Warteschlange
    //    so fortsetzen, wie es das Spiel selbst tut. Der Auftrag bleibt, Zutaten werden erst beim Start genommen.
    //  - Wartet auf einen Zombie (Abholen/Ausgabe), obwohl keiner mehr da ist -> Auftrag abschliessen, weitermachen.
    //  - Fertig, aber "Ausgabe verzoegert": ist jetzt Platz, abschliessen; sonst sagen, was fehlt.
    //  - Erweiterung fehlt (z. B. Werkzeugkiste steht daneben, ist aber nicht verbunden) -> neu verbinden.
    //  - Zu wenig Talent, Energie, Brennstoff, Platz: nur Hinweis, nichts wird veraendert.
    internal static class CraftRepair
    {
        private static readonly Dictionary<string, float> lastLog = new Dictionary<string, float>();

        internal static int Fix(Wgo w, string why)
        {
            WgoData d = w != null ? w.Data : null;
            if (d == null || !Plugin.RepairWorkbenches.Value) return 0;
            CraftComponent cc = d.CraftComponent;
            if (cc == null || cc.CraftElementsQueue == null || cc.CraftElementsQueue.Count == 0) return 0;
            if (d.CraftableType != CraftableType.Regular || d.CraftableAttachedWorker is ZombieWgoData || cc.IsDestroyingCraftActive) return 0;

            string state = Describe(d, cc);
            CraftElementBase cur = cc.CurrentCraftElement;
            CraftElementBase front = cc.CraftElementsQueue.FirstOrDefault(x => x != null);
            CraftComponentStatus st = cc.Status;

            // 0) nicht beim Herstell-System des Spiels angemeldet (dann wird z. B. ein fertiger Durchgang nie abgeschlossen)
            //    -> anmelden, wie es das Spiel beim Laden fuer jede Werkbank mit Warteschlange tut
            bool registered = true;
            try { registered = MainGame.Instance.GameSave.craftSystemData.activeCrafts?.Contains(cc) ?? false; } catch { }
            bool reRegistered = false;
            if (!registered && cc.ShouldRegisterInCraftSystem())
            {
                MainGame.Instance.craftSystem.AddCraftObject(cc);
                reRegistered = true;
                Plugin.Log.LogInfo("Craft repair (" + why + "): " + ((ObjectLinkedToDefinition<WGODef>)d).id + " - registered in the game's craft system again");
            }

            // 1) Zombie-Wartezustand ohne Zombie
            if ((st == CraftComponentStatus.WaitingForWorkerPickUp || st == CraftComponentStatus.WaitingForOutputDrop) && cur != null)
            {
                cc.TryFinishCurCraft();
                return Done(d, why, state, "finished the craft that waited for a zombie (" + st + ") -> " + cc.Status,
                    Labels.T("Werkbank wartete auf einen Zombie, der nicht mehr da ist – Auftrag abgeschlossen, es geht weiter.",
                             "The workbench was waiting for a zombie that is gone – craft completed, it continues."));
            }

            // 2) fertig, Ausgabe verzoegert
            if (st == CraftComponentStatus.FinishDelayed && cur != null)
            {
                CraftStatus fs = cur.CanFinishCraft(cc.CraftableObject);
                if (fs == CraftStatus.OK)
                {
                    cc.TryFinishCurCraft();
                    return Done(d, why, state, "finished the delayed craft -> " + cc.Status,
                        Labels.T("Fertiger Auftrag hing bei der Ausgabe – jetzt abgeschlossen.", "A finished craft was stuck at the output – completed now."));
                }
                return Hint(d, why, state, cur, fs);
            }

            // 3) steht auf "laeuft" / fertig / abgebrochen, aber der aktuelle Auftrag ist nicht gestartet (piermaz38)
            bool waits = st == CraftComponentStatus.QueueDelayed || st == CraftComponentStatus.ReadyToStartCraft || st == CraftComponentStatus.ReadyToFinishAutoCraft;
            if (!waits && (cur == null || !cur.IsStarted))
            {
                cc.Status = CraftComponentStatus.QueueDelayed;
                cc.TryContinueFromQueue();
                try { MainGame.Instance.craftSystem.AddCraftObject(cc); } catch { }
                return Done(d, why, state, "restarted the queue (status " + st + " without a running craft) -> " + cc.Status + ", current " + Short(cc.CurrentCraftElement),
                    Labels.T("Werkbank neu angestoßen: Sie stand auf „läuft“, ohne dass etwas lief. Es geht nichts verloren.",
                             "Workbench restarted: it was stuck as \"running\" with nothing running. Nothing is lost."));
            }

            // 4) vorne wartet ein Auftrag, der nicht starten kann
            if (front != null && !front.IsStarted)
            {
                CraftStatus s = cc.GetStartCraftStatus(front);
                if (s == CraftStatus.NotEnoughResources || s == CraftStatus.DoesntHaveItemWithEnoughDurability || s == CraftStatus.DoesntHaveRequiredTool)
                {
                    bool auto = front.Def is CraftDef cd && (cd.isAuto || cd.isObjDestroyCraft);
                    if (!auto && (front.CraftInput == null || front.CraftInput.Count == 0))
                    {
                        cc.RemoveFromQueue(front);
                        if (!cc.CraftElementsQueue.Contains(front))
                            return Done(d, why, state, "removed waiting " + front.CraftId + " x" + front.Count + " (" + s + ")",
                                string.Format(Labels.T("Wartenden Auftrag „{0}“ herausgenommen ({1}). Es geht nichts verloren.", "Took the waiting craft \"{0}\" out ({1}). Nothing is lost."), Name(front), Reason(s)));
                    }
                }
                else if (s == CraftStatus.NoExtension && Relink(w, d))
                {
                    return Done(d, why, state, "re-linked the extensions (" + front.CraftId + " needs " + front.Def?.extensionNeedId + ")",
                        Labels.T("Erweiterungen der Werkbank neu verbunden (z. B. Werkzeugkiste) – bitte noch einmal F drücken.",
                                 "Re-linked the workbench extensions (e.g. tool box) – please press F once more."));
                }
                else if (s != CraftStatus.OK && !cc.CraftElementsQueue.Any(x => x != null && !x.IsStarted && cc.GetStartCraftStatus(x) == CraftStatus.OK))
                    return Hint(d, why, state, front, s);
            }

            // 5) laeuft, aber das Talent reicht nicht (jeder Schlag bringt 0 Fortschritt)
            if (cur != null && cur.IsStarted && LacksMastery(d, cur)) return Hint(d, why, state, cur, CraftStatus.NotEnoughMastery);

            if (reRegistered)
                return Done(d, why, state, "nothing else to fix",
                    Labels.T("Werkbank war beim Spiel abgemeldet – wieder angemeldet. Es geht nichts verloren.",
                             "The workbench had dropped out of the game's crafting – registered again. Nothing is lost."));

            // nichts gefunden: nur protokollieren (fuer Fehlerberichte) und kurz Bescheid geben
            if (!Log(d, why, state + " -> nothing to fix")) return 0;
            if (!(st == CraftComponentStatus.Started && cur != null && cur.IsStarted))
                ManualSave.Toast(Labels.T("Werkbank-Prüfung: keine Blockade gefunden. Details stehen im Log (LogOutput.log).",
                                          "Workbench check: no blockage found. Details are in the log (LogOutput.log)."), 4f);
            return 0;
        }

        private static int Done(WgoData d, string why, string state, string action, string toast)
        {
            Plugin.Log.LogInfo("Craft repair (" + why + "): " + state);
            Plugin.Log.LogInfo("Craft repair (" + why + "): " + ((ObjectLinkedToDefinition<WGODef>)d).id + " - " + action);
            lastLog[KeyOf(d)] = Time.realtimeSinceStartup;
            ManualSave.Toast(toast, 6f);
            return 1;
        }

        private static int Hint(WgoData d, string why, string state, CraftElementBase e, CraftStatus s)
        {
            if (!Log(d, why, state + " -> hint " + s)) return 0;
            ManualSave.Toast(string.Format(Labels.T("Werkbank-Prüfung „{0}“: {1}.", "Workbench check \"{0}\": {1}."), Name(e), Reason(s)), 6f);
            return 0;
        }

        // hoechstens alle 5 s je Werkbank (F mehrmals hintereinander)
        private static bool Log(WgoData d, string why, string line)
        {
            string k = KeyOf(d);
            if (lastLog.TryGetValue(k, out float t) && Time.realtimeSinceStartup - t < 5f) return false;
            lastLog[k] = Time.realtimeSinceStartup;
            Plugin.Log.LogInfo("Craft check (" + why + "): " + line);
            return true;
        }

        private static string KeyOf(WgoData d) { try { return d.UniqueId.ToString(); } catch { return ((ObjectLinkedToDefinition<WGODef>)d).id; } }

        internal static string Reason(CraftStatus s)
        {
            switch (s)
            {
                case CraftStatus.NotEnoughResources: return Labels.T("Zutaten fehlen", "ingredients missing");
                case CraftStatus.DoesntHaveRequiredTool: return Labels.T("Werkzeug fehlt", "tool missing");
                case CraftStatus.DoesntHaveItemWithEnoughDurability: return Labels.T("Werkzeug ist abgenutzt", "tool is worn out");
                case CraftStatus.NotEnoughEnergy: return Labels.T("zu wenig Energie", "not enough energy");
                case CraftStatus.NotEnoughTalent:
                case CraftStatus.NotEnoughMastery: return Labels.T("dafür brauchst du eine höhere Talent-Stufe", "you need a higher talent level for this");
                case CraftStatus.NotEnoughFuel: return Labels.T("Brennstoff fehlt", "fuel missing");
                case CraftStatus.NotEnoughSpaceInWgo:
                case CraftStatus.NotEnoughSpaceInMultiInventory: return Labels.T("kein Platz für das Ergebnis", "no room for the result");
                case CraftStatus.NoExtension: return Labels.T("eine Erweiterung fehlt (z. B. Werkzeugkiste)", "an extension is missing (e.g. tool box)");
                default: return string.Format(Labels.T("Spiel meldet {0}", "game reports {0}"), s);
            }
        }

        private static string Name(CraftElementBase e)
        {
            try { if (e?.Def is CraftDef cd) return Pins.Loc(Pins.CraftTitleKey(cd)); } catch { }
            return e?.CraftId ?? "?";
        }

        private static bool LacksMastery(WgoData d, CraftElementBase e)
        {
            try
            {
                CraftDefBase def = e?.Def;
                if (def == null || def.isAuto || def.isStarCraft || def.isAutopsyCraft || MainGame.PlayerController == null) return false;
                return MainGame.PlayerController.GetMasteryLevelForTalentBranch(d.Definition.talent, def) < def.talentLock;
            }
            catch { return false; }
        }

        // wie beim Bauen (und wie BuildMove nach dem Verschieben): Erweiterungen in Reichweite neu verbinden
        private static bool Relink(Wgo w, WgoData d)
        {
            try
            {
                WGODef logic = GameBalance.Me.GetWorkbenchExtensionLogicDef(((ObjectLinkedToDefinition<WGODef>)d).id);
                if (logic == null || !GameBalance.Me.workbenchesWhichUseExtensions.Contains(logic)) return false;
                var m = AccessTools.Method(typeof(Wgo), "TryRegisterWorkbenchExtensionDelayed");
                if (m == null) return false;
                w.StartCoroutine((IEnumerator)m.Invoke(w, new object[] { true }));
                return true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Craft repair: relink " + e.Message); return false; }
        }

        private static string Short(CraftElementBase e)
        {
            if (e == null) return "-";
            string p = "";
            try { p = " " + e.ProgressTicks + "/" + e.TotalProgressTicks; } catch { }
            return e.CraftId + " x" + e.Count + (e.IsStarted ? " started" : " waiting") + p;
        }

        // kompletter Zustand fuer das Log (Fehlerberichte)
        internal static string Describe(WgoData d, CraftComponent cc)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(((ObjectLinkedToDefinition<WGODef>)d).id).Append(" status=").Append(cc.Status);
            try { sb.Append(" current=").Append(Short(cc.CurrentCraftElement)); } catch { }
            sb.Append(" queue=[");
            int i = 0;
            foreach (CraftElementBase e in cc.CraftElementsQueue)
            {
                if (i++ > 0) sb.Append(" | ");
                if (e == null) { sb.Append("null"); continue; }
                sb.Append(Short(e));
                try { if (!e.IsStarted) sb.Append(" ").Append(cc.GetStartCraftStatus(e)); } catch { }
                try { if (e.CraftInput != null && e.CraftInput.Count > 0) sb.Append(" input=").Append(e.CraftInput.Count); } catch { }
            }
            sb.Append("]");
            try { sb.Append(" preFinish=").Append(cc.HasPreFinishUpdate).Append(" held=").Append(cc.IsPreFinishHeld); } catch { }
            try { sb.Append(" registered=").Append(MainGame.Instance.GameSave.craftSystemData.activeCrafts?.Contains(cc) ?? false); } catch { }
            try { sb.Append(" worker=").Append(d.Worker == null ? "-" : d.Worker.GetType().Name); } catch { }
            try { sb.Append(" extensions=").Append(d.AttachedWorkbenchExtensions.Count); } catch { }
            return sb.ToString();
        }

        internal static Wgo WgoOf(CraftInteractionHandler h) => Traverse.Create(h).Field("assignedWgo").GetValue<Wgo>();
    }

    // F an der Werkbank (Interact) - vor dem Spiel aufraeumen, damit das Fenster ganz normal aufgeht
    [HarmonyPatch(typeof(CraftInteractionHandler), nameof(CraftInteractionHandler.Interact))]
    internal static class CraftRepairPatch
    {
        private static void Prefix(CraftInteractionHandler __instance)
        {
            try { CraftRepair.Fix(CraftRepair.WgoOf(__instance), "use"); }
            catch (Exception e) { Plugin.Log.LogWarning("Craft repair: " + e.Message); }
        }
    }
}
