using System.Collections.Generic;
using BepInEx.Configuration;

namespace GK2Tweaks
{
    // Reihenfolge der Werte in der FPS-Anzeige (Config "Overlay/Order", im Mod-Menue per Pfeil verschiebbar)
    internal static class OverlayOrder
    {
        internal const string Default = "Fps,Lows,FrameTime,Cpu,Gpu,GpuTemp,Ram,Vram,Resolution,Clock,Weekday,GameTime";
        private static readonly string[] all = Default.Split(',');
        private static string cachedRaw;
        private static List<string> cached;

        internal static ConfigEntry<bool> Entry(string key)
        {
            switch (key)
            {
                case "Fps": return Plugin.OvFps;
                case "Lows": return Plugin.OvLows;
                case "FrameTime": return Plugin.OvFrameTime;
                case "Cpu": return Plugin.OvCpu;
                case "Gpu": return Plugin.OvGpu;
                case "GpuTemp": return Plugin.OvGpuTemp;
                case "Ram": return Plugin.OvRam;
                case "Vram": return Plugin.OvVram;
                case "Resolution": return Plugin.OvResolution;
                case "Clock": return Plugin.OvClock;
                case "Weekday": return Plugin.OvWeekday;
                case "GameTime": return Plugin.OvGameTime;
                default: return null;
            }
        }

        // gespeicherte Reihenfolge, unbekannte Eintraege raus, fehlende hinten angehaengt
        internal static List<string> Get()
        {
            string raw = Plugin.OvOrder.Value ?? "";
            if (cached != null && raw == cachedRaw) return cached;
            var list = new List<string>();
            foreach (string p in raw.Split(','))
            {
                string k = p.Trim();
                if (System.Array.IndexOf(all, k) >= 0 && !list.Contains(k)) list.Add(k);
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (list.Contains(all[i])) continue;
                int after = i > 0 ? list.IndexOf(all[i - 1]) : -1;
                list.Insert(after + 1, all[i]);
            }
            cachedRaw = raw;
            cached = list;
            return list;
        }

        internal static void Move(int index, int delta)
        {
            var list = new List<string>(Get());
            int j = index + delta;
            if (index < 0 || index >= list.Count || j < 0 || j >= list.Count) return;
            string t = list[index]; list[index] = list[j]; list[j] = t;
            Plugin.OvOrder.Value = string.Join(",", list.ToArray());
        }
    }
}
