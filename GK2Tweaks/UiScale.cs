using System;
using HarmonyLib;

namespace GK2Tweaks
{
    // "HUD & Menues: Groesse": Ab Spiel 1.011 berechnet das Spiel den Faktor fuer HUD, Menues und Fenster an einer Stelle,
    // ResolutionConfig.GetUiLayout - aus der Pixelgroesse der Spielwelt (Ultrawide oft 3x, 5120x1440 sogar 4x). Wir setzen
    // dort statt der Pixelgroesse die gewaehlte Stufe ein. Passt sie nicht auf den Bildschirm, nimmt das Spiel wie gewohnt die
    // naechstkleinere (seine eigene Logik bleibt unveraendert). Die Spielwelt bleibt gleich - dafuer gibt es den Kamera-Zoom
    // bzw. die (x2)-Aufloesungen von GK2 Ultrawide. Vor 1.011 gibt es GetUiLayout nicht: dann bleibt die Option still aus.
    internal static class UiScale
    {
        internal static int Wanted => Plugin.GameUiScale == null ? 0 : Plugin.GameUiScale.Value;

        // Sofort anwenden: wie bei einem Aufloesungswechsel benachrichtigt das Spiel HUD, Fenster, Cursor und Kamera
        internal static void Apply()
        {
            SafeMode.Run("UiScale", () =>
            {
                GameSettings gs = GameSettings.Instance;
                if (gs != null) gs.NotifyResolutionChanged();
                Plugin.Log.LogInfo("HUD & menu size: " + (Wanted == 0 ? "like the game" : Wanted + "x") + " (now " + ResolutionConfig.GetUiScaleFactor() + "x)");
            });
        }
    }

    [HarmonyPatch(typeof(ResolutionConfig), nameof(ResolutionConfig.GetUiLayout))]
    internal static class UiScalePatch
    {
        private static void Prefix(ref int worldPixelSize)
        {
            try
            {
                if (!SafeMode.On("UiScale")) return;
                int s = UiScale.Wanted;
                if (s > 0) worldPixelSize = s;
            }
            catch { }
        }
    }
}
