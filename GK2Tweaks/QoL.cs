using System;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Kamera-Zoom: das Spiel berechnet die Kamerahoehe nur aus der Aufloesung; wir multiplizieren sie danach.
    [HarmonyPatch(typeof(CameraSystem), nameof(CameraSystem.OnResolutionChanged))]
    internal static class ZoomPatch
    {
        private static void Postfix(CameraSystem __instance, IntVector2 res)
        {
            float zoom = Plugin.Zoom.Value / 100f;
            if (Mathf.Approximately(zoom, 1f)) return;
            float size = CameraSystem.CalculateOrthographicSize(res.y, ResolutionConfig.PixelSize) / zoom;
            __instance.SetOrthographicSize(size);
            if (__instance.WorldCamera != null) __instance.WorldCamera.orthographicSize = size;
        }

        internal static void Reapply()
        {
            try
            {
                if (CameraSystem.Instance != null && GameSettings.Instance != null)
                    CameraSystem.Instance.OnResolutionChanged(GameSettings.Instance.GetResolutionIntVector2());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Zoom: " + e.Message); }
        }
    }

    // Autosave: speichert alle N Minuten, aber nur wenn der Spieler frei steuerbar ist (kein Menue, kein Schlaf, keine Szene).
    internal static class AutoSave
    {
        private static float nextSave = -1f;

        internal static void Tick()
        {
            int minutes = Plugin.AutoSaveMinutes.Value;
            if (minutes <= 0 || Plugin.BenchOn) { nextSave = -1f; return; }
            MainGame mg = MainGame.Instance;
            if (mg == null || mg.gameState != MainGame.GameState.InGame) { nextSave = -1f; return; }
            float now = Time.realtimeSinceStartup;
            if (nextSave < 0f) { nextSave = now + minutes * 60f; return; }
            if (now < nextSave) return;
            try
            {
                if (!MainGame.PlayerController.IsControlsEnabledExcept()) { nextSave = now + 10f; return; }
                Plugin.Log.LogInfo("Autosave");
                SaveSystem.Save(mg.SaveSlotData, mg.GameSave);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Autosave fehlgeschlagen: " + e.Message); }
            nextSave = now + minutes * 60f;
        }
    }

    // Manuelles Speichern (Button im Mod-Menue oder Taste). Speichert nur im laufenden Spiel und wenn der Spieler frei steuerbar ist.
    internal static class ManualSave
    {
        internal static string Message = "";
        internal static float MessageUntil;
        private static bool busy;

        internal static bool ShowMessage => Time.realtimeSinceStartup < MessageUntil;

        private static void Say(string text) => Toast(text, 3f);

        internal static void Toast(string text, float seconds)
        {
            Message = text;
            MessageUntil = Time.realtimeSinceStartup + seconds;
        }

        internal static void Save(bool menuOpen, Action onSaved = null)
        {
            if (busy) return;
            try
            {
                MainGame mg = MainGame.Instance;
                if (mg == null || mg.gameState != MainGame.GameState.InGame || mg.GameSave == null)
                { Say(Labels.T("Speichern geht nur im laufenden Spiel.", "Saving only works while playing.")); return; }
                // Das offene Mod-Menue sperrt selbst die Steuerung (ByTeleport) - das zaehlt hier nicht.
                bool free = menuOpen ? MainGame.PlayerController.IsControlsEnabledExcept(TakenControlType.ByTeleport)
                                     : MainGame.PlayerController.IsControlsEnabledExcept();
                if (!free) { Say(Labels.T("Gerade nicht möglich (Dialog, Arbeit, Schlaf …). Kurz warten und nochmal.", "Not possible right now (dialogue, work, sleep …). Try again in a moment.")); return; }
                busy = true;
                Say(Labels.T("Speichere …", "Saving …"));
                Plugin.Log.LogInfo("Manual save");
                SaveSystem.Save(mg.SaveSlotData, mg.GameSave,
                    () => { busy = false; Say(Labels.T("Gespeichert ", "Saved ") + DateTime.Now.ToString("HH:mm")); onSaved?.Invoke(); },
                    () => { busy = false; Say(Labels.T("Speichern fehlgeschlagen.", "Saving failed.")); });
            }
            catch (Exception e)
            {
                busy = false;
                Plugin.Log.LogWarning("Manual save: " + e.Message);
                Say(Labels.T("Speichern fehlgeschlagen.", "Saving failed."));
            }
        }
    }
}
