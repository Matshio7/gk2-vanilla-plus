using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GK2Ultrawide
{
    [BepInPlugin("mats.gk2.ultrawide", "GK2 Ultrawide", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<string> CustomResolution;
        internal static ConfigEntry<string> MainMenuScaleX2;

        private void Awake()
        {
            Log = Logger;
            CustomResolution = Config.Bind("General", "CustomResolution", "",
                "Additional resolution(s) for the graphics settings, comma separated, e.g. 3840x1080,5120x1440. Empty = only resolutions reported by the monitor. / Zusaetzliche Aufloesung(en), komma-getrennt. Leer = nur vom Monitor gemeldete.");
            MainMenuScaleX2 = Config.Bind("General", "MainMenuScaleX2", "Auto",
                new ConfigDescription("Main menu scene scaling for unlocked resolutions (Auto/On/Off). Auto = same rule the game uses for similar resolutions. / Skalierung der Hauptmenue-Szene.",
                    new AcceptableValueList<string>("Auto", "On", "Off")));
            new Harmony("mats.gk2.ultrawide").PatchAll();
            Log.LogInfo("GK2 Ultrawide loaded");
        }
    }

    // Das Spiel verwirft in InitAvailableResolutions alle Aufloesungen mit Seitenverhaeltnis > 2:1
    // sowie Breiten > 5120. Wir fuegen diese danach wieder hinzu.
    [HarmonyPatch(typeof(ResolutionConfig), nameof(ResolutionConfig.InitAvailableResolutions))]
    internal static class InitAvailableResolutionsPatch
    {
        private static bool done;

        private static void Postfix()
        {
            if (done) return;
            done = true;

            var candidates = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions) candidates.Add(new Vector2Int(r.width, r.height));
            if (Display.main != null) candidates.Add(new Vector2Int(Display.main.systemWidth, Display.main.systemHeight));
            candidates.Add(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));

            var custom = new HashSet<Vector2Int>();
            foreach (string part in Plugin.CustomResolution.Value.Split(','))
            {
                string[] wh = part.Trim().ToLowerInvariant().Split('x');
                if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h) && w > 0 && h > 0)
                {
                    var v = new Vector2Int(w, h);
                    custom.Add(v);
                    candidates.Add(v);
                }
            }

            var hardcoded = Traverse.Create(typeof(ResolutionConfig)).Field("hardcodedResolutions").GetValue<List<ResolutionConfig>>();
            var available = Traverse.Create(typeof(ResolutionConfig)).Field("availableResolutions").GetValue<List<ResolutionConfig>>();
            // Meldet der Monitor (z.B. unter Wine) keine Aufloesungen, nutzt das Spiel die Hardcoded-Liste als Fallback.
            // Diese Liste uebernehmen, damit unsere Eintraege sie nicht verdraengen.
            if (available.Count == 0)
                foreach (ResolutionConfig r in hardcoded) ResolutionConfig.TryAddAvailableResolution(r);
            var seen = new HashSet<Vector2Int>();
            foreach (Vector2Int c in candidates)
            {
                if (!seen.Add(c) || c.y < 720) continue;
                bool skippedByGame = (float)c.x / c.y > 2f || c.x > 5120;
                if (!skippedByGame && !custom.Contains(c)) continue;

                List<ResolutionConfig> matches = hardcoded.FindAll(r => r.ListedWidth == c.x && r.ListedHeight == c.y);
                if (matches.Count > 0)
                {
                    foreach (ResolutionConfig m in matches) ResolutionConfig.TryAddAvailableResolution(m);
                }
                else
                {
                    var cfg = new ResolutionConfig(c.x, c.y);
                    int logicalHeight = c.y / ResolutionConfig.GetPixelSize(c.x, c.y);
                    string mode = Plugin.MainMenuScaleX2.Value;
                    bool scaleX2 = mode == "On" || (mode == "Auto" && logicalHeight >= 600);
                    Traverse.Create(cfg).Field("useMainMenuScaleX2").SetValue(scaleX2);
                    ResolutionConfig.TryAddAvailableResolution(cfg);
                }
                Plugin.Log.LogInfo($"Resolution unlocked: {c.x}x{c.y}");
            }
        }
    }
}
