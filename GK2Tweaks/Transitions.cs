using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Profiling;

namespace GK2Tweaks
{
    // Schnellere Uebergaenge (Tueren, Kartenreise). Bei jedem Teleport blendet das Spiel ab (0,3 s), wartet
    // (delayInFade 0,3 s), raeumt den kompletten Speicher auf (MainGame.HiddenOptimization: UnloadUnusedAssets +
    // GC.Collect) und blendet wieder auf (0,3 s).
    //  Stufe 1 (Standard an): Blenden und Pause nur waehrend eines Teleports auf rund ein Drittel.
    //  Stufe 2 (Standard aus): Speicher-Aufraeumen bei Tueren nur noch alle 10 Minuten oder wenn der Speicher
    //  knapp wird. Beim Laden eines Spielstands wird immer aufgeraeumt.
    // Idee aus dem Nexus-Mod "Instant Transitions" von LeBetoven - eigene Umsetzung.
    internal static class Transitions
    {
        private const float Window = 6f;          // so lange nach einem Teleport gelten Blenden als "Teleport-Blenden"
        private const float CleanupEvery = 600f;  // Stufe 2: spaetestens alle 10 Minuten aufraeumen
        private static float teleportAt = -100f, lastCleanup = -1000f;
        private static bool measuring, cleanupSkipped, otherChecked;
        private static readonly List<float> times = new List<float>();

        internal static bool OtherMod { get; private set; }
        internal static int Count => times.Count;
        internal static float Average { get { float s = 0f; foreach (float t in times) s += t; return times.Count > 0 ? s / times.Count : 0f; } }

        private static bool InTeleport => Time.unscaledTime - teleportAt < Window;

        private static bool Fast
        {
            get
            {
                if (!otherChecked)
                {
                    otherChecked = true;
                    OtherMod = ModCompat.Has("instant", "transition");
                    if (OtherMod) Plugin.Log.LogInfo("Transitions: 'Instant Transitions' is installed, own changes stay off");
                }
                return !OtherMod && SafeMode.On("Transitions");
            }
        }

        internal static void OnTeleport(TeleportDataBase d)
        {
            teleportAt = Time.unscaledTime;
            measuring = true;
            cleanupSkipped = false;
            if (d != null && !d.donNotFade && Plugin.FasterTransitions.Value && Fast && d.delayInFade > 0.1f) d.delayInFade = 0.1f;
        }

        internal static float Scale(float t)
        {
            if (!Plugin.FasterTransitions.Value || !InTeleport || t <= 0.12f || !Fast) return t;
            return Mathf.Max(0.1f, t * 0.35f);
        }

        // Ende des Uebergangs = erstes Aufblenden nach dem Teleport -> Dauer messen
        internal static Action WrapDone(Action a)
        {
            if (!measuring || !InTeleport) return a;
            measuring = false;
            float start = teleportAt;
            bool skipped = cleanupSkipped;
            return () =>
            {
                float d = Time.unscaledTime - start;
                times.Add(d);
                if (times.Count > 10) times.RemoveAt(0);
                Plugin.Log.LogInfo("[TRANSITION] " + d.ToString("0.00") + " s" + (Plugin.FasterTransitions.Value && Fast ? " (faster fades" + (skipped ? ", cleanup skipped" : "") + ")" : ""));
                a?.Invoke();
            };
        }

        // true = Aufraeumen diesmal auslassen
        internal static bool SkipCleanup()
        {
            float now = Time.unscaledTime;
            if (!Plugin.LessMemoryCleanup.Value || !InTeleport || !Fast || now - lastCleanup > CleanupEvery || MemoryTight())
            {
                lastCleanup = now;
                return false;
            }
            cleanupSkipped = true;
            return true;
        }

        private static bool MemoryTight()
        {
            try
            {
                long used = Profiler.GetTotalAllocatedMemoryLong();
                long sys = (long)SystemInfo.systemMemorySize * 1024L * 1024L;
                return sys <= 0 || used > sys * 45 / 100;
            }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Teleport))]
    internal static class TeleportPatch
    {
        private static void Prefix(TeleportDataBase teleportData)
        {
            try { Transitions.OnTeleport(teleportData); } catch (Exception e) { SafeMode.Fail("Transitions", e); }
        }
    }

    [HarmonyPatch(typeof(UIBasicFade), nameof(UIBasicFade.FadeIn), new[] { typeof(float), typeof(Action), typeof(FadeFlag), typeof(bool) })]
    internal static class FadeInPatch
    {
        private static void Prefix(ref float fadeTime)
        {
            try { fadeTime = Transitions.Scale(fadeTime); } catch { }
        }
    }

    [HarmonyPatch(typeof(UIBasicFade), nameof(UIBasicFade.FadeOut), new[] { typeof(float), typeof(Action), typeof(FadeFlag) })]
    internal static class FadeOutPatch
    {
        private static void Prefix(ref float fadeTime, ref Action onComplete)
        {
            try { fadeTime = Transitions.Scale(fadeTime); onComplete = Transitions.WrapDone(onComplete); } catch { }
        }
    }

    [HarmonyPatch(typeof(MainGame), nameof(MainGame.HiddenOptimization))]
    internal static class CleanupPatch
    {
        private static bool Prefix(ref UniTask __result)
        {
            try
            {
                if (!Transitions.SkipCleanup()) return true;
                __result = UniTask.CompletedTask;
                return false;
            }
            catch { return true; }
        }
    }
}
