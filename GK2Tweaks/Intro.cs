using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology.Preloader;
using UnityEngine;
using UnityEngine.Video;

namespace GK2Tweaks
{
    // Logos/Intro-Videos beim Start ueberspringen. Das eigentliche Laden laeuft weiter, nur die Wartezeit entfaellt.
    [HarmonyPatch(typeof(LazyPreloader), "RunLogoCoroutine")]
    internal static class SkipLogosPatch
    {
        private static void Prefix(LazyPreloader __instance)
        {
            if (!Plugin.SkipIntro.Value) return;
            try
            {
                var list = Traverse.Create(__instance).Field("logoList").GetValue<List<LazyLogoData>>();
                if (list == null) return;
                foreach (LazyLogoData l in list)
                {
                    l.fadeInTime = 0f;
                    l.fadeOutTime = 0f;
                    l.showLengthMode = LazyLogoData.ShowLengthMode.TimeLimited;
                    l.showingTime = 0f;
                }
                Plugin.Log.LogInfo("Logos uebersprungen (" + list.Count + ")");
            }
            catch (Exception e) { Plugin.Log.LogWarning("SkipIntro: " + e.Message); }
        }

        // Der Preloader startet schon in Awake der ersten Szene, also bevor BepInEx den Mod laedt.
        // Deshalb zusaetzlich zur Laufzeit: Rest-Logos auf 0 setzen, Videos ans Ende spulen und
        // solange Logos laufen die Spielzeit beschleunigen (WaitForSeconds/Fades nutzen skalierte Zeit).
        private static bool done, patchedList, sped;
        private static float oldScale = 1f;

        internal static void Tick()
        {
            if (done) return;
            if (!Plugin.SkipIntro.Value || Time.realtimeSinceStartup > 60f) { Finish(); return; }
            try
            {
                LazyPreloader p = UnityEngine.Object.FindFirstObjectByType<LazyPreloader>();
                if (p == null || !p.isActiveAndEnabled) { if (sped || Time.realtimeSinceStartup > 20f) Finish(); return; }
                if (!patchedList) { Prefix(p); patchedList = true; }
                VideoPlayer vp = Traverse.Create(p).Field("videoPlayer").GetValue<VideoPlayer>();
                if (vp != null && vp.isPlaying && vp.clip != null && vp.time < vp.clip.length - 0.2)
                    vp.time = vp.clip.length - 0.1;
                if (!sped) { oldScale = Time.timeScale; Time.timeScale = 50f; sped = true; Plugin.Log.LogInfo("Logos: Schnelldurchlauf"); }
            }
            catch (Exception e) { Plugin.Log.LogWarning("SkipIntro: " + e.Message); Finish(); }
        }

        private static void Finish()
        {
            if (sped) { Time.timeScale = oldScale > 0f && oldScale < 10f ? oldScale : 1f; Plugin.Log.LogInfo("Logos: fertig"); }
            sped = false;
            done = true;
        }
    }
}
