using System.Collections.Generic;
using UnityEngine;

namespace GK2Tweaks
{
    // OLED-Schwarz: Der Bereich ausserhalb der Karte (leere Flaeche um Innenraeume/Levelrand) wird mit der
    // Hintergrundfarbe der Kamera geloescht, im Spiel ein dunkles Grau. Mit dem Schalter wird er echt schwarz.
    // Nur Bildschirm-Kameras (ohne RenderTexture), Originalwerte werden beim Ausschalten wiederhergestellt.
    internal static class Oled
    {
        private struct Orig { public CameraClearFlags flags; public Color color; }
        private static readonly Dictionary<Camera, Orig> changed = new Dictionary<Camera, Orig>();
        private static float next;
        private static Camera[] buf = new Camera[16];

        internal static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            if (Plugin.OledBlack.Value) Apply(); else Restore();
        }

        private static void Apply()
        {
            int n = Camera.allCamerasCount;
            if (buf.Length < n) buf = new Camera[n + 8];
            n = Camera.GetAllCameras(buf);
            for (int i = 0; i < n; i++)
            {
                Camera c = buf[i];
                if (c == null || c.targetTexture != null) continue;
                if (c.clearFlags != CameraClearFlags.SolidColor && c.clearFlags != CameraClearFlags.Skybox) continue;
                if (c.clearFlags == CameraClearFlags.SolidColor && c.backgroundColor == Color.black) continue;
                if (!changed.ContainsKey(c)) changed[c] = new Orig { flags = c.clearFlags, color = c.backgroundColor };
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = Color.black;
            }
        }

        private static void Restore()
        {
            if (changed.Count == 0) return;
            foreach (var kv in changed)
            {
                if (kv.Key == null) continue;
                kv.Key.clearFlags = kv.Value.flags;
                kv.Key.backgroundColor = kv.Value.color;
            }
            changed.Clear();
        }
    }
}
