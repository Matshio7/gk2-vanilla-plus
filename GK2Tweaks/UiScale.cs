using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    // "HUD & Menues: Groesse": Ab Spiel 1.011 berechnet das Spiel den Faktor fuer HUD, Menues und Fenster an einer Stelle,
    // ResolutionConfig.GetUiLayout - aus der Pixelgroesse der Spielwelt (Ultrawide oft 3x, 5120x1440 sogar 4x). Wir setzen
    // dort statt der Pixelgroesse die gewaehlte Stufe ein (1x bis 4x). Passt sie nicht auf den Bildschirm, passt das Spiel sie
    // nach seinen eigenen Regeln an (seine Logik bleibt unveraendert) - das Mod-Menue zeigt dann die wirksame Groesse.
    // Die Spielwelt bleibt gleich - dafuer gibt es den Kamera-Zoom
    // bzw. die (x2)-Aufloesungen von GK2 Ultrawide. Vor 1.011 gibt es GetUiLayout nicht: dann bleibt die Option still aus.
    internal static class UiScale
    {
        internal static int Wanted => Plugin.GameUiScale == null ? 0 : Plugin.GameUiScale.Value;

        // Text fuer das Mod-Menue: bei "Wie im Spiel" die Groesse des Spiels, bei einer zu grossen Stufe die wirksame.
        // Nur fuer den aktuell eingestellten Wert (nur dafuer stimmt GetUiScaleFactor), sonst nur die Stufe.
        internal static string ValueText(int v)
        {
            int eff = 0;
            try { if (v == Wanted && SafeMode.On("UiScale")) eff = Effective(); } catch { eff = 0; }
            if (v == 0) return eff > 0 ? string.Format(Labels.T("Wie im Spiel ({0}×)", "Like the game ({0}×)"), eff) : Labels.T("Wie im Spiel", "Like the game");
            if (eff > 0 && eff != v) return string.Format(Labels.T("{0}× – zu groß, {1}×", "{0}× – too big, {1}×"), v, eff);
            return v + "×";
        }

        // Eigene Methode: vor Spiel 1.011 fehlt GetUiScaleFactor - der Fehler beim JIT faellt dann in das try oben
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Effective() => Mathf.RoundToInt(ResolutionConfig.GetUiScaleFactor());

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
