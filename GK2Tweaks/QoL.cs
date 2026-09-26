using System;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Kamera-Zoom: das Spiel berechnet die Kamerahoehe nur aus der Aufloesung; wir teilen sie danach durch den Zoom.
    [HarmonyPatch(typeof(CameraSystem), nameof(CameraSystem.OnResolutionChanged))]
    internal static class ZoomPatch
    {
        private static void Postfix(CameraSystem __instance, IntVector2 res)
        {
            // Hauptmenue, Ladebildschirm usw. immer mit 100 % - das Menuebild ist auf diese Groesse gebaut
            if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.InGame) return;
            CameraZoom.Apply(__instance, res.y, CameraZoom.Current);
        }

        private static int lastState = -1;

        // Beim Wechsel Hauptmenue <-> Spiel die Kamera neu berechnen lassen
        internal static void Tick()
        {
            MainGame mg = MainGame.Instance;
            int s = mg == null ? -1 : (int)mg.gameState;
            if (s == lastState) return;
            lastState = s;
            Reapply();
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

    // Zoom-Presets per Taste, eigener Zoom fuer Innenraeume, stufenloses Zoomen mit dem Mausrad, weiche Uebergaenge.
    internal static class CameraZoom
    {
        internal static float Current = 100f;       // aktuell angezeigter Zoom in %
        private static float user = -1f;            // Zoom draussen (Einstellung, Preset oder Mausrad)
        private static float inside = -1f;          // Zoom in Innenraeumen (0 = wie draussen)
        private static float applied = -1f;
        private static bool indoor;
        private static float indoorCheckAt;

        internal static void OnSettingsChanged()
        {
            user = Plugin.Zoom.Value;
            inside = Plugin.InteriorZoom.Value;
        }

#if DEV
        internal static void TestSet(float v) { if (user < 0f) OnSettingsChanged(); user = v; }
#endif
        private static float Target => indoor && inside > 0f ? inside : user;

        internal static void Apply(CameraSystem cs, int resY, float zoomPercent)
        {
            float zoom = zoomPercent / 100f;
            float size = CameraSystem.CalculateOrthographicSize(resY, ResolutionConfig.PixelSize) / zoom;
            cs.SetOrthographicSize(size);
            if (cs.WorldCamera != null) cs.WorldCamera.orthographicSize = size;
            applied = zoomPercent;
        }

        internal static void Tick(float dt)
        {
            if (user < 0f) OnSettingsChanged();
            MainGame mg = MainGame.Instance;
            if (mg == null || mg.gameState != MainGame.GameState.InGame || CameraSystem.Instance == null || GameSettings.Instance == null) { applied = -1f; return; }

            if (Time.realtimeSinceStartup >= indoorCheckAt) { indoorCheckAt = Time.realtimeSinceStartup + 0.5f; indoor = IsIndoor(); }

            if (Plugin.ZoomPresetKey.Value.MainKey != KeyCode.None && Plugin.ZoomPresetKey.Value.IsDown()) NextPreset();
            if (Plugin.MouseWheelZoom.Value) Wheel();

            float target = Target;
            // nach dem Laden ohne Animation direkt auf den Zielwert
            if (applied < 0f) Current = target;
            else Current = Plugin.SmoothZoom.Value ? Mathf.Lerp(Current, target, 1f - Mathf.Exp(-dt * 12f)) : target;
            if (Mathf.Abs(Current - target) < 0.05f) Current = target;
            if (Mathf.Abs(Current - applied) > 0.01f)
            {
                try { Apply(CameraSystem.Instance, GameSettings.Instance.GetResolutionIntVector2().y, Current); }
                catch (Exception e) { Plugin.Log.LogWarning("Zoom: " + e.Message); applied = Current; }
            }
        }

        private static void NextPreset()
        {
            var list = new System.Collections.Generic.List<float>();
            foreach (string part in (Plugin.ZoomPresets.Value ?? "").Split(','))
                if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v >= 30f && v <= 300f) list.Add(v);
            if (list.Count == 0) return;
            float cur = indoor && inside > 0f ? inside : user;
            float next = list[0];
            for (int i = 0; i < list.Count; i++) if (Mathf.Abs(list[i] - cur) < 0.5f) { next = list[(i + 1) % list.Count]; break; }
            SetActive(next);
        }

        private static void Wheel()
        {
            float d = Input.mouseScrollDelta.y;
            if (Mathf.Abs(d) < 0.01f) return;
            if (Plugin.Instance.Gui.AnyWindowOpen) return;
            try
            {
                if (!MainGame.PlayerController.IsControlsEnabledExcept()) return;
                if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            }
            catch { return; }
            float cur = indoor && inside > 0f ? inside : user;
            SetActive(Mathf.Clamp(cur * Mathf.Pow(1.08f, d), 50f, 200f), toast: false);
        }

        private static void SetActive(float v, bool toast = true)
        {
            if (indoor && inside > 0f) inside = v; else user = v;
            if (toast) ManualSave.Toast(Labels.T("Zoom ", "Zoom ") + Mathf.RoundToInt(v) + " %" + (indoor && inside > 0f ? Labels.T(" (innen)", " (indoors)") : ""), 1.5f);
        }

        // Innenraum: Spielerposition liegt in einer der Innenraum-Flaechen des Spiels (wie beim Teleport-Graph)
        private static bool IsIndoor()
        {
            if (Plugin.InteriorZoom.Value <= 0) return false;
            try
            {
                Vector3 pos = MainGame.PlayerController.transform.position;
                foreach (GameSceneConfig cfg in MainGame.Instance.gameSceneConfigs)
                {
                    if (cfg == null) continue;
                    foreach (IndoorAreaData a in cfg.IndoorAreas)
                        if (a != null && a.ContainsXZ(pos)) return true;
                }
            }
            catch { }
            return false;
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
