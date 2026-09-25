#if DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine.LowLevel;

namespace GK2Tweaks
{
    // Nur im Messmodus: misst, wie viel Zeit jede Phase der Unity-Schleife pro Frame braucht.
    internal static class LoopProfiler
    {
        private sealed class Marker { }

        private static string[] names;
        private static long[] begin, total, max;
        private static bool installed;
        internal static bool Active;

        internal static void Install()
        {
            if (installed) return;
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            var nameList = new List<string>();
            PlayerLoopSystem[] phases = root.subSystemList;
            var layout = new StringBuilder("[BENCH] LOOP root:");
            foreach (PlayerLoopSystem ph in phases)
            {
                layout.Append(' ').Append(ph.type?.Name ?? "?").Append('(').Append(ph.subSystemList?.Length ?? 0).Append(')');
                if (ph.subSystemList != null && ph.subSystemList.Length > 0)
                    layout.Append('[').Append(ph.subSystemList[0].type?.Name).Append("..").Append(ph.subSystemList[ph.subSystemList.Length - 1].type?.Name).Append(']');
            }
            Plugin.Log.LogInfo(layout.ToString());
            for (int p = 0; p < phases.Length; p++)
            {
                PlayerLoopSystem phase = phases[p];
                if (phase.subSystemList == null) continue;
                var list = new List<PlayerLoopSystem>(phase.subSystemList.Length * 3);
                foreach (PlayerLoopSystem sub in phase.subSystemList)
                {
                    int idx = nameList.Count;
                    nameList.Add((phase.type?.Name ?? "?") + "." + (sub.type?.Name ?? "?"));
                    list.Add(new PlayerLoopSystem { type = typeof(Marker), updateDelegate = () => Begin(idx) });
                    list.Add(sub);
                    list.Add(new PlayerLoopSystem { type = typeof(Marker), updateDelegate = () => End(idx) });
                }
                phase.subSystemList = list.ToArray();
                phases[p] = phase;
            }
            root.subSystemList = phases;
            names = nameList.ToArray();
            begin = new long[names.Length];
            gapBefore = new long[names.Length];
            total = new long[names.Length];
            max = new long[names.Length];
            PlayerLoop.SetPlayerLoop(root);
            installed = true;
        }

        internal static void Reset()
        {
            if (!installed) return;
            Array.Clear(total, 0, total.Length);
            Array.Clear(max, 0, max.Length);
            Array.Clear(begin, 0, begin.Length);
            Array.Clear(gapBefore, 0, gapBefore.Length);
            outsideTotal = outsideMax = lastEnd = lastMarker = 0;
        }

        private static long lastEnd, outsideTotal, outsideMax, lastMarker;
        private static long[] gapBefore;

        private static void Begin(int i)
        {
            if (!Active) { lastEnd = 0; lastMarker = 0; return; }
            long now = Stopwatch.GetTimestamp();
            if (i == 0 && lastEnd != 0)
            {
                long gap = now - lastEnd;
                outsideTotal += gap;
                if (gap > outsideMax) outsideMax = gap;
            }
            else if (lastMarker != 0) gapBefore[i] += now - lastMarker;
            begin[i] = now;
        }

        private static void End(int i)
        {
            if (!Active || begin[i] == 0) return;
            long now = Stopwatch.GetTimestamp();
            long d = now - begin[i];
            begin[i] = 0;
            total[i] += d;
            if (d > max[i]) max[i] = d;
            lastMarker = now;
            if (i == names.Length - 1) lastEnd = now;
        }

        internal static string Report(int frames, int top)
        {
            if (!installed || frames <= 0) return "loop=n/a";
            double f = 1000.0 / Stopwatch.Frequency;
            long inside = 0;
            for (int i = 0; i < total.Length; i++) inside += total[i];
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "insideLoop={0:0.00}ms outsideLoop={1:0.00}/{2:0.0}ms loop:",
                inside * f / frames, outsideTotal * f / frames, outsideMax * f);
            foreach (int i in Enumerable.Range(0, names.Length).OrderByDescending(i => total[i]).Take(top))
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1:0.00}/{2:0.0}ms", names[i], total[i] * f / frames, max[i] * f);
            long gaps = 0;
            for (int i = 0; i < gapBefore.Length; i++) gaps += gapBefore[i];
            sb.AppendFormat(CultureInfo.InvariantCulture, " | gapsTotal={0:0.00}ms gapBefore:", gaps * f / frames);
            foreach (int i in Enumerable.Range(0, names.Length).OrderByDescending(i => gapBefore[i]).Take(8))
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1:0.00}ms", names[i], gapBefore[i] * f / frames);
            return sb.ToString();
        }
    }

    // Nur im Messmodus: Zeit je Spielsystem aus dem UpdateManager (Handwerk, Foerderbaender, Bewegung usw.).
    [HarmonyPatch(typeof(ScheduledUpdate), nameof(ScheduledUpdate.CallUpdate))]
    internal static class SystemProfiler
    {
        private sealed class Stat { public long Total, Max; public int Calls; }
        private static readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        internal static bool Active;

        private static bool Prefix(ScheduledUpdate __instance, float deltaTime, bool applyTimeMultiplier)
        {
            if (!Active) return true;
            if (applyTimeMultiplier) deltaTime *= MainGame.UpdateManager.TimeMultiplier;
            foreach (ICustomUpdatable u in __instance.customUpdatables)
            {
                long t0 = Stopwatch.GetTimestamp();
                u.CustomUpdate(deltaTime);
                long d = Stopwatch.GetTimestamp() - t0;
                string key = u.GetType().Name;
                if (!stats.TryGetValue(key, out Stat s)) stats[key] = s = new Stat();
                s.Total += d;
                s.Calls++;
                if (d > s.Max) s.Max = d;
            }
            return false;
        }

        internal static void Reset() => stats.Clear();

        internal static string Report(int frames)
        {
            if (frames <= 0 || stats.Count == 0) return "systems=n/a";
            double f = 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder("systems:");
            foreach (KeyValuePair<string, Stat> kv in stats.OrderByDescending(k => k.Value.Total).Take(12))
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1:0.00}/{2:0.0}ms/{3}x", kv.Key, kv.Value.Total * f / frames, kv.Value.Max * f, kv.Value.Calls);
            return sb.ToString();
        }
    }
}

#endif
