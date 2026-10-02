using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2Tweaks
{
    // Abbauen im Bau-Modus. Im Spiel legt "Abriss" einen Abbau-Craft an, zu dem die Figur hinlaeuft, und gibt
    // dabei nur einen Teil des Materials zurueck.
    // - Sofort abbauen: diesen Craft sofort beenden (die Figur muss nicht hin, z. B. an den Ruinen kommt sie nie an).
    //   Das Spiel verteilt Material und Inventar selbst und entfernt das Objekt (gleicher Weg wie normal).
    // - Volle Erstattung (nicht mehr ganz Vanilla): die Ausgabe des Abbau-Crafts wird auf die vollen Baukosten
    //   aufgefuellt. Inhalt (Inventar, Brennstoff) kommt wie im Spiel zusaetzlich zurueck.
    [HarmonyPatch(typeof(Wgo), nameof(Wgo.DoBuildRemove))]
    internal static class InstantRemovePatch
    {
        private static void Prefix(Wgo __instance, out bool __state)
        {
            __state = false;
            try
            {
                // Nur wenn noch kein Abbau laeuft (sonst ist der Klick ein "Abbruch" und bleibt wie im Spiel)
                __state = (Plugin.InstantRemove.Value || Plugin.FullRefund.Value) && __instance != null && __instance.Data != null
                          && __instance.Data.CraftComponent != null && !__instance.Data.CraftComponent.IsDestroyingCraftActive;
            }
            catch { __state = false; }
        }

        private static void Postfix(Wgo __instance, ref bool __result, bool __state)
        {
            if (!__state || __result) return;
            try
            {
                CraftComponent cc = __instance.Data.CraftComponent;
                if (cc == null || !cc.IsDestroyingCraftActive) return;
                if (Plugin.FullRefund.Value) TopUpRefund(__instance, cc);
                if (!Plugin.InstantRemove.Value) return;
                cc.TryFinishCurCraft();
                __result = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Building removal: " + e.Message); }
        }

        private static string Id(Item it) => ((ObjectLinkedToDefinition<ItemDef>)it).id;

        private static void TopUpRefund(Wgo w, CraftComponent cc)
        {
            GameBalance.Me.buildableWgos.TryGetValue(w.Id, out BuildingDef def);
            if (def == null || def.needItems == null || def.needItems.Count == 0) return;
            object ce = cc.CurrentCraftElement;
            if (ce == null) return;
            Traverse f = Traverse.Create(ce).Field("customCraftOutput");
            var output = f.GetValue<List<Item>>();
            if (output == null) { output = new List<Item>(); f.SetValue(output); }

            // Was schon in der Ausgabe steckt, abzueglich des Objekt-Inhalts (der kommt ohnehin zurueck)
            var have = new Dictionary<string, int>();
            foreach (Item it in output)
                if (it != null && !it.IsEmpty) { have.TryGetValue(Id(it), out int n); have[Id(it)] = n + it.Count; }
            if (w.Data.Inventory != null && w.Data.Inventory.Data != null)
                foreach (Item it in w.Data.Inventory.Data.Inventory)
                    if (it != null && !it.IsEmpty && have.ContainsKey(Id(it))) have[Id(it)] = Math.Max(0, have[Id(it)] - it.Count);

            int added = 0;
            foreach (NeedItemData need in def.needItems)
            {
                if (need == null || string.IsNullOrEmpty(need.id)) continue;
                int want = need.GetCount();
                have.TryGetValue(need.id, out int got);
                if (want > got) { output.Add(new Item(need.id, want - got)); added += want - got; }
            }
            if (added > 0) Plugin.Log.LogInfo("Full refund: +" + added + " item(s) for " + w.Id);
        }
    }
}
