using System;
using HarmonyLib;

namespace GK2Tweaks
{
    // Sofort abbauen: Im Abriss-Modus legt das Spiel einen "Abbau-Craft" an, zu dem die Figur hinlaeuft.
    // Liegt das Objekt z. B. an den Ruinen, kommt sie nie an. Mit der Option beenden wir genau diesen Craft
    // sofort - das Spiel verteilt dann selbst Material und Inventar und entfernt das Objekt (gleicher Weg wie normal).
    [HarmonyPatch(typeof(Wgo), nameof(Wgo.DoBuildRemove))]
    internal static class InstantRemovePatch
    {
        private static void Prefix(Wgo __instance, out bool __state)
        {
            __state = false;
            try
            {
                // Nur wenn noch kein Abbau laeuft (sonst ist der Klick ein "Abbruch" und bleibt wie im Spiel)
                __state = Plugin.InstantRemove.Value && __instance != null && __instance.Data != null
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
                cc.TryFinishCurCraft();
                __result = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Instant removal: " + e.Message); }
        }
    }
}
