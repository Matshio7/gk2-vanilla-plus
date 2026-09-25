using System;
using System.Collections.Generic;
using System.Globalization;

namespace GK2Tweaks
{
    internal struct FrameResult
    {
        public int Frames;
        public double AvgFps, Low1Fps, Low01Fps, AvgMs, SdMs, MaxMs, Over20Pct, Over25Pct, Over34Pct;
        public int Over50;

        public string ToLogString() => string.Format(CultureInfo.InvariantCulture,
            "frames={0} avg={1:0.0}fps p1={2:0.0}fps p01={3:0.0}fps avgMs={4:0.00} sdMs={5:0.00} maxMs={6:0.0} gt20={7:0.0}% gt25={8:0.0}% gt34={9:0.0}% gt50={10}",
            Frames, AvgFps, Low1Fps, Low01Fps, AvgMs, SdMs, MaxMs, Over20Pct, Over25Pct, Over34Pct, Over50);
    }

    internal sealed class FrameStats
    {
        private readonly List<float> ms = new List<float>(8192);

        public int Count => ms.Count;
        public void Add(float dtSeconds) => ms.Add(dtSeconds * 1000f);
        public void Reset() => ms.Clear();

        public FrameResult Compute()
        {
            var r = new FrameResult { Frames = ms.Count };
            int n = ms.Count;
            if (n == 0) return r;
            float[] s = ms.ToArray();
            Array.Sort(s);
            double sum = 0;
            int o20 = 0, o25 = 0, o34 = 0, o50 = 0;
            for (int i = 0; i < n; i++)
            {
                float v = s[i];
                sum += v;
                if (v > 20f) o20++;
                if (v > 25f) o25++;
                if (v > 34f) o34++;
                if (v > 50f) o50++;
            }
            double avg = sum / n, var = 0;
            for (int i = 0; i < n; i++) { double d = s[i] - avg; var += d * d; }
            r.AvgMs = avg;
            r.AvgFps = 1000.0 * n / sum;
            r.SdMs = Math.Sqrt(var / n);
            r.MaxMs = s[n - 1];
            r.Low1Fps = 1000.0 / s[Math.Min(n - 1, (int)Math.Ceiling(n * 0.99) - 1)];
            r.Low01Fps = 1000.0 / s[Math.Min(n - 1, (int)Math.Ceiling(n * 0.999) - 1)];
            r.Over20Pct = 100.0 * o20 / n;
            r.Over25Pct = 100.0 * o25 / n;
            r.Over34Pct = 100.0 * o34 / n;
            r.Over50 = o50;
            return r;
        }
    }
}
