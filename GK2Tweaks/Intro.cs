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
        // Spiel-Update 1.011 (Workshop-Sync, Mod-Menue): der Ablauf beim Start kann sich geaendert haben. Der 50x-Schnelllauf
        // darf deshalb nie haengen bleiben - harte Grenzen, nach denen immer Finish() (Zeitskala zurueck) kommt:
        //  - nach MaxSeconds Echtzeit seit Spielstart, egal ob der Preloader noch laeuft (45 s: langsame Erststarts mit HDD
        //    oder Shader-Cache bekommen den Schnelllauf noch; der Waechter stellt die Zeitskala in jedem Fall zurueck),
        //  - sobald MainGame im Hauptmenue/im Spiel ist - aber erst, wenn der Preloader weg ist oder der Schnelllauf schon
        //    MenuCheckDelay laeuft (falls MainMenu der Standardwert des Enums ist und MainGame schon waehrend der Logos existiert),
        //  - bei jedem Fehler,
        //  - zusaetzlich ein eigener Koroutinen-Waechter: der laeuft auch, wenn Update() vorher abbricht oder
        //    SafeMode "SkipLogos" abschaltet und Tick() deshalb nicht mehr aufgerufen wird.
        private const float MaxSeconds = 45f;
        private const float MenuCheckDelay = 3f;
        private static bool done, patchedList, sped, watchdog;
        private static float oldScale = 1f, spedAt;

        internal static void Tick()
        {
            if (done) return;
            if (!Plugin.SkipIntro.Value || Time.realtimeSinceStartup > MaxSeconds) { Finish(); return; }
            try
            {
                LazyPreloader p = null;
                try { p = UnityEngine.Object.FindFirstObjectByType<LazyPreloader>(); } catch (Exception e) { Plugin.Log.LogWarning("SkipIntro: " + e.Message); Finish(); return; }
                if (p == null || !p.isActiveAndEnabled)
                {
                    // Preloader (noch) nicht da oder schon fertig
                    if (sped || Time.realtimeSinceStartup > 20f || MenuReached()) Finish();
                    return;
                }
                if (SpedLongEnough() && MenuReached()) { Finish(); return; }
                if (!patchedList) { Prefix(p); patchedList = true; }
                VideoPlayer vp = Traverse.Create(p).Field("videoPlayer").GetValue<VideoPlayer>();
                if (vp != null && vp.isPlaying && vp.clip != null && vp.time < vp.clip.length - 0.2)
                    vp.time = vp.clip.length - 0.1;
                if (!sped)
                {
                    StartWatchdog();
                    oldScale = Time.timeScale; Time.timeScale = 50f; sped = true; spedAt = Time.realtimeSinceStartup; Plugin.Log.LogInfo("Logos: Schnelldurchlauf");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("SkipIntro: " + e.Message); Finish(); }
        }

        private static bool SpedLongEnough() => sped && Time.realtimeSinceStartup - spedAt >= MenuCheckDelay;

        // MainGame existiert und ist schon im Hauptmenue oder im Spiel -> kein Grund mehr fuer den Schnelllauf.
        // Nicht inline: fehlt MainGame nach einem Update, wirft erst der Aufruf (und wird dort abgefangen).
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool MenuReached()
        {
            try
            {
                MainGame mg = MainGame.Instance;
                return mg != null && (mg.gameState == MainGame.GameState.MainMenu || mg.gameState == MainGame.GameState.InGame);
            }
            catch { return false; }
        }

        private static void StartWatchdog()
        {
            if (watchdog) return;
            try { if (Plugin.Instance != null) { Plugin.Instance.StartCoroutine(Watchdog()); watchdog = true; } }
            catch (Exception e) { Plugin.Log.LogWarning("SkipIntro watchdog: " + e.Message); }
        }

        // Die Pruefung steckt in einem eigenen try (ein JIT-/TypeLoad-Fehler von MenuReached wuerde sonst die Koroutine
        // beenden), und finally stellt die Zeitskala in jedem Fall zurueck.
        private static System.Collections.IEnumerator Watchdog()
        {
            try
            {
                while (!done && Time.realtimeSinceStartup <= MaxSeconds + 1f)
                {
                    bool reached = false;
                    try { reached = SpedLongEnough() && MenuReached(); } catch { reached = false; }
                    if (reached) break;
                    yield return null;
                }
            }
            finally
            {
                if (!done)
                {
                    try { Plugin.Log.LogWarning("Logos: Waechter stellt die Spielzeit wieder her"); } catch { }
                    Finish();
                }
            }
        }

        private static void Finish()
        {
            if (sped)
            {
                // nur zuruecksetzen, solange noch unser Wert gilt - hat das Spiel inzwischen selbst eine Zeitskala
                // gesetzt (z.B. 0 fuer Pause/Dialog), wird sie nicht ueberschrieben
                try { if (Mathf.Abs(Time.timeScale - 50f) < 0.01f) Time.timeScale = oldScale > 0f && oldScale < 10f ? oldScale : 1f; } catch { }
                try { Plugin.Log.LogInfo("Logos: fertig"); } catch { }
            }
            sped = false;
            done = true;
        }
    }
}
