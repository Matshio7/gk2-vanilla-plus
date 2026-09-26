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
        private static bool logged;
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
            // Die Weltkamera rendert in eine RenderTexture (wird danach auf den Bildschirm kopiert) - sie gezielt einschliessen
            Camera world = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : null;
            if (!logged && world != null)
            {
                logged = true;
                for (int i = 0; i < n; i++)
                    if (buf[i] != null)
                        Plugin.Log.LogInfo("OLED cams: " + buf[i].name + " depth " + buf[i].depth + " " + buf[i].clearFlags + " " + buf[i].backgroundColor + (buf[i].targetTexture != null ? " RT" : "") + (buf[i] == world ? " WORLD" : ""));
            }
            for (int i = 0; i < n; i++)
            {
                Camera c = buf[i];
                if (c == null || (c.targetTexture != null && c != world)) continue;
                if (c.clearFlags != CameraClearFlags.SolidColor && c.clearFlags != CameraClearFlags.Skybox) continue;
                if (c.clearFlags == CameraClearFlags.SolidColor && c.backgroundColor == Color.black) continue;
                if (!changed.ContainsKey(c))
                {
                    changed[c] = new Orig { flags = c.clearFlags, color = c.backgroundColor };
                    Plugin.Log.LogInfo("OLED black: " + c.name + " " + c.clearFlags + " " + c.backgroundColor + " -> black");
                }
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
