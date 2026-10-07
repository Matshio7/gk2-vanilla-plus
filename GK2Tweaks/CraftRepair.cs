using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2Tweaks
{
    // 1.7.2: haengende Herstell-Auftraege loesen. Wird ein Auftrag mit mehr Durchgaengen angelegt, als Zutaten da sind
    // (mit "+" im Spiel genauso moeglich, mit dem Max-Knopf bis 1.7.1 aber leicht), bleibt der Rest als "wartet" vorne in
    // der Warteschlange der Werkbank stehen. Die Werkbank reagiert dann nicht mehr auf F und laesst sich nicht abbauen -
    // und das steckt im Spielstand (bleibt auch ohne Mod).
    // Loesung: Sobald der Spieler eine solche Werkbank benutzt oder abbaut, werden NICHT GESTARTETE Auftraege, denen
    // Zutaten / Werkzeug fehlen, aus der Warteschlange genommen. Dabei geht nichts verloren: Zutaten werden im Spiel erst
    // beim Start eines Durchgangs verbraucht. Nur Werkbaenke, die der Spieler selbst bedient (kein Zombie, keine Fabrik).
    internal static class CraftRepair
    {
        internal static int Fix(WgoData d, string why)
        {
            if (d == null) return 0;
            CraftComponent cc = d.CraftComponent;
            if (cc == null || cc.CraftElementsQueue == null || cc.CraftElementsQueue.Count == 0) return 0;
            if (d.CraftableType != CraftableType.Regular || d.CraftableAttachedWorker is ZombieWgoData || cc.IsDestroyingCraftActive) return 0;
            int n = 0;
            foreach (CraftElementBase e in new List<CraftElementBase>(cc.CraftElementsQueue))
            {
                if (e == null || e.IsStarted || (e.CraftInput != null && e.CraftInput.Count > 0)) continue;
                if (e.Def is CraftDef cd && (cd.isAuto || cd.isObjDestroyCraft)) continue;
                CraftStatus s = cc.GetStartCraftStatus(e);
                if (s != CraftStatus.NotEnoughResources && s != CraftStatus.DoesntHaveItemWithEnoughDurability && s != CraftStatus.DoesntHaveRequiredTool) continue;
                cc.RemoveFromQueue(e);
                if (!cc.CraftElementsQueue.Contains(e))
                {
                    n++;
                    Plugin.Log.LogInfo("Craft repair (" + why + "): " + ((ObjectLinkedToDefinition<WGODef>)d).id + " - removed waiting " + e.CraftId + " x" + e.Count + " (" + s + ")");
                }
            }
            return n;
        }

        internal static Wgo WgoOf(CraftInteractionHandler h) => Traverse.Create(h).Field("assignedWgo").GetValue<Wgo>();
    }

    // F an der Werkbank (Interact) - vor dem Spiel aufraeumen, damit das Fenster ganz normal aufgeht
    [HarmonyPatch(typeof(CraftInteractionHandler), nameof(CraftInteractionHandler.Interact))]
    internal static class CraftRepairPatch
    {
        private static void Prefix(CraftInteractionHandler __instance)
        {
            try { CraftRepair.Fix(CraftRepair.WgoOf(__instance)?.Data, "use"); }
            catch (Exception e) { Plugin.Log.LogWarning("Craft repair: " + e.Message); }
        }
    }
}
