using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GK2Tweaks
{
    // Grafik-Benchmark fuer Spieler: geht alle Grafikstufen des Spiels nacheinander durch (plus die eigenen
    // Einstellungen), misst jeweils FPS und 1%-Low bei freier Bildrate und gibt am Ende Score und Empfehlung aus.
    // Nichts wird gespeichert, die alte Grafikstufe wird danach wiederhergestellt. Esc bricht ab.
    internal static class GraphicsBench
    {
        internal sealed class Step
        {
            public string Name;
            public GraphicsTier Tier;
            public bool Mine;          // eigene Einstellungen (Stufe + Mod-Overrides)
            public double Fps, Low;
        }

        private const float Warmup = 3f, Measure = 10f;

        internal static bool Running { get; private set; }
        internal static bool SuppressOverrides => Running && cur >= 0 && cur < steps.Count && !steps[cur].Mine;
        internal static List<Step> Results = new List<Step>();
        internal static int Score;
        internal static string Recommendation = "";
        internal static string Status = "";

        private static readonly List<Step> steps = new List<Step>();
        private static int cur = -1;
        private static float phaseEnd;
        private static bool measuring;
        private static readonly FrameStats stats = new FrameStats();
        private static GraphicsTier originalTier;

        internal static bool CanRun => WeekPlan.InGame && GameSettings.Instance != null;

        internal static void Start()
        {
            if (Running || !CanRun) return;
            GameSettings gs = GameSettings.Instance;
            originalTier = gs.graphicsTier;
            steps.Clear();
            foreach (GraphicsTier t in new[] { GraphicsTier.Lowest, GraphicsTier.Low, GraphicsTier.Medium, GraphicsTier.High })
                steps.Add(new Step { Name = Labels.Tier(t), Tier = t });
            if (Overrides.Any())
                steps.Add(new Step { Name = Labels.T("Deine Einstellungen", "Your settings") + " (" + Labels.Tier(originalTier) + " + Mod)", Tier = originalTier, Mine = true });
            Running = true;
            cur = -1;
            Plugin.Instance.Gui.SetMenu(false);
            try { MainGame.PlayerController?.SetControlTakenType(TakenControlType.ByTeleport, false); } catch { }
            Unlock();
            Next();
            Plugin.Log.LogInfo("[GFXBENCH] start, " + steps.Count + " steps, " + Screen.width + "x" + Screen.height + ", " + SystemInfo.graphicsDeviceName);
        }

        private static void Next()
        {
            cur++;
            if (cur >= steps.Count) { Finish(false); return; }
            Step s = steps[cur];
            GameSettings gs = GameSettings.Instance;
            gs.graphicsTier = s.Tier;
            try { gs.ApplyGraphicsTier(applySave: false); } catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
            Unlock();
            measuring = false;
            stats.Reset();
            phaseEnd = Time.realtimeSinceStartup + Warmup;
        }

        // freie Bildrate waehrend der Messung, sonst misst man nur das FPS-Limit
        internal static void Unlock()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        internal static void Tick(float dt)
        {
            if (!Running) return;
            if (Input.GetKeyDown(KeyCode.Escape) || !CanRun) { Finish(true); return; }
            float now = Time.realtimeSinceStartup;
            Step s = steps[cur];
            if (measuring) stats.Add(dt);
            float left = Mathf.Max(0f, phaseEnd - now);
            Status = Labels.T("Grafik-Benchmark", "Graphics benchmark") + "  " + (cur + 1) + "/" + steps.Count + ":  " + s.Name + "   "
                + (measuring ? Labels.T("misst … ", "measuring … ") : Labels.T("wartet … ", "warming up … ")) + Mathf.CeilToInt(left) + " s"
                + "     (Esc = " + Labels.T("Abbrechen", "cancel") + ")";
            if (now < phaseEnd) return;
            if (!measuring) { measuring = true; stats.Reset(); phaseEnd = now + Measure; return; }
            FrameResult r = stats.Compute();
            s.Fps = r.AvgFps;
            s.Low = r.Low1Fps;
            Plugin.Log.LogInfo("[GFXBENCH] " + s.Tier + (s.Mine ? " (mine)" : "") + " " + r.ToLogString() + " features=[" + Plugin.DescribeFeatures() + "]");
            Next();
        }

        private static void Finish(bool cancelled)
        {
            Running = false;
            Status = "";
            GameSettings gs = GameSettings.Instance;
            if (gs != null)
            {
                gs.graphicsTier = originalTier;
                try { gs.ApplyGraphicsTier(applySave: false); } catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
            }
            Plugin.ReapplyPacing();
            try { MainGame.PlayerController?.SetControlTakenType(TakenControlType.ByTeleport, true); } catch { }
            if (cancelled)
            {
                ManualSave.Toast(Labels.T("Benchmark abgebrochen.", "Benchmark cancelled."), 3f);
                Plugin.Log.LogInfo("[GFXBENCH] cancelled");
                return;
            }
            Results = new List<Step>(steps);
            Evaluate();
            WriteFile();
            Plugin.Instance.Gui.SetMenu(true);
            Plugin.Instance.Gui.ShowBenchResults();
        }

        // Score: geometrisches Mittel der FPS ueber die vier Spielstufen x 10 (unabhaengig von Monitor und Limit).
        // Empfehlung: hoechste Stufe, die das Ziel im Schnitt haelt (>= 95 %) und keine groben Einbrueche hat (1%-Low >= 60 %).
        private static void Evaluate()
        {
            double logSum = 0; int n = 0;
            foreach (Step s in Results) if (!s.Mine && s.Fps > 0) { logSum += Math.Log(s.Fps); n++; }
            Score = n > 0 ? (int)Math.Round(Math.Exp(logSum / n) * 10) : 0;
            int target = TargetFps();
            Step best = null;
            foreach (Step s in Results) if (!s.Mine && s.Fps >= target * 0.95 && s.Low >= target * 0.6) best = s; // Reihenfolge Niedrigste -> Hoch
            Step mine = Results.Find(x => x.Mine);
            string mineNote = mine != null && mine.Fps >= target * 0.95 && mine.Low >= target * 0.6
                ? Labels.T("  Deine eigenen Einstellungen halten das Ziel ebenfalls.", "  Your own settings hold the target too.") : "";
            Recommendation = (best != null
                ? Labels.T("Empfehlung: ", "Recommended: ") + best.Name + Labels.T(" (hält " + target + " FPS)", " (holds " + target + " FPS)")
                : Labels.T("Empfehlung: Niedrigste – " + target + " FPS werden auf keiner Stufe ganz gehalten. Tipp: Render-Modus \"Pixel\" oder eine kleinere Auflösung.",
                           "Recommended: Lowest – no tier fully holds " + target + " FPS. Tip: render mode \"Pixel\" or a lower resolution.")) + mineNote;
        }

        internal static int TargetFps()
        {
            if (Plugin.Pacing.Value != "Game" && Plugin.TargetFps.Value > 0) return Plugin.TargetFps.Value;
            int hz = Plugin.MonitorHz();
            return hz > 0 ? Math.Min(hz, 60) : 60;
        }

        private static void WriteFile()
        {
            try
            {
                string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus");
                Directory.CreateDirectory(dir);
                var lines = new List<string>
                {
                    "=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  GK2 Vanilla+ " + Plugin.PluginVersion + "  ===",
                    SystemInfo.graphicsDeviceName + " | " + SystemInfo.processorType + " | " + Screen.width + "x" + Screen.height + " | " + SystemInfo.graphicsDeviceVersion,
                };
                foreach (Step s in Results)
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "{0,-40} {1,6:0.0} FPS   1% low {2,6:0.0}", s.Name, s.Fps, s.Low));
                lines.Add("Score " + Score + "   " + Recommendation);
                lines.Add("");
                File.AppendAllLines(Path.Combine(dir, "benchmark.txt"), lines);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Benchmark file: " + e.Message); }
        }
    }
}
