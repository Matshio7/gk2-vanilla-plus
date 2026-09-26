using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

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
            if (Plugin.OledBlack.Value) { Apply(); ApplyGrading(); } else { Restore(); RestoreGrading(); }
            if (Plugin.OledBlack.Value && diagPending && CameraSystem.Instance != null && CameraSystem.Instance.MainCamera != null)
            {
                diagPending = false;
                Plugin.Instance.StartCoroutine(Diagnose(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            }
        }

        // Screenshot-Taste bei aktivem OLED-Schwarz: einmal Diagnose ins Log
        internal static bool diagPending;

        // Das eigentliche Grau: Der Farbfilter des Spiels (Color Grading, LDR) hat Kontrast -10, das hebt Schwarz auf
        // Mittelgrau * 0.1 (~ 5/255) an. Ausgleich ueber "Lift" (Offset, der zu Weiss hin auf 0 auslaeuft):
        // Schwarz -> 0, Weiss bleibt unveraendert, Mitteltoene aendern sich kaum (~1 %).
        private static ColorGrading gradedCg;
        private static Vector4 origLift, setLift;
        private static bool origLiftOverride;

        private static void ApplyGrading()
        {
            var cs = CameraSystem.Instance;
            var vol = cs != null && cs.MainCamera != null ? cs.MainCamera.PostProcessVolume : null;
            ColorGrading cg = vol != null && vol.profile != null ? vol.profile.GetSetting<ColorGrading>() : null;
            if (cg == null) return;
            if (cg != gradedCg)
            {
                RestoreGrading();
                gradedCg = cg;
                origLift = cg.lift.value;
                origLiftOverride = cg.lift.overrideState;
            }
            else if (cg.lift.value != setLift) origLift = cg.lift.value; // vom Spiel geaendert
            float contrast = cg.contrast.value / 100f + 1f;
            float black = contrast < 1f ? Mathf.Pow(0.5f, 2.2f) * (1f - contrast) * 1.1f : 0f;
            float w = black > 0f ? -black / (1f - black) : 0f;
            setLift = new Vector4(origLift.x, origLift.y, origLift.z, origLift.w + w);
            if (cg.lift.value != setLift)
            {
                cg.lift.overrideState = true;
                cg.lift.value = setLift;
                Plugin.Log.LogInfo("OLED black: grading lift " + origLift.w + " -> " + setLift.w + " (contrast " + cg.contrast.value + ")");
            }
        }

        private static void RestoreGrading()
        {
            if (gradedCg != null && gradedCg.lift.value == setLift)
            {
                gradedCg.lift.value = origLift;
                gradedCg.lift.overrideState = origLiftOverride;
            }
            gradedCg = null;
        }

        // Einmal pro Szene ins Log: Farben an Rand/Mitte im Welt-Bild (nach Post-Processing) und auf dem Bildschirm,
        // plus aktive Post-Processing-Effekte - um herauszufinden, woher das Grau kommt.
        private static System.Collections.IEnumerator Diagnose(string scene)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                MainCamera mc = CameraSystem.Instance.MainCamera;
                RenderTexture rt = mc.RenderTexture;
                var pts = new List<Vector2>(); for (int gy = 0; gy < 5; gy++) for (int gx = 0; gx < 8; gx++) pts.Add(new Vector2(0.02f + gx * 0.96f / 7f, 0.02f + gy * 0.96f / 4f));
                var sb = new System.Text.StringBuilder("OLED diag [" + scene + "]");
                if (rt != null)
                {
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    sb.Append(" RT " + rt.width + "x" + rt.height + ":");
                    foreach (Vector2 p in pts)
                    {
                        tex.ReadPixels(new Rect((int)(p.x * (rt.width - 1)), (int)(p.y * (rt.height - 1)), 1, 1), 0, 0);
                        Color32 c = tex.GetPixel(0, 0);
                        sb.Append(" (" + c.r + "," + c.g + "," + c.b + "," + c.a + ")");
                    }
                    RenderTexture.active = prev;
                    Object.Destroy(tex);
                }
                Texture2D scr = ScreenCapture.CaptureScreenshotAsTexture();
                if (scr != null)
                {
                    sb.Append(" | Screen:");
                    foreach (Vector2 p in pts)
                    {
                        Color32 c = scr.GetPixel((int)(p.x * (scr.width - 1)), (int)(p.y * (scr.height - 1)));
                        sb.Append(" (" + c.r + "," + c.g + "," + c.b + ")");
                    }
                    Object.Destroy(scr);
                }
                var vol = mc.PostProcessVolume;
                if (vol != null && vol.profile != null)
                {
                    sb.Append(" | PP weight " + vol.weight + ":");
                    foreach (var s in vol.profile.settings)
                        if (s != null) sb.Append(" " + s.GetType().Name + (s.enabled.value ? "" : "(off)"));
                }
                ColorGrading cg = vol != null && vol.profile != null ? vol.profile.GetSetting<ColorGrading>() : null;
                if (cg != null)
                {
                    sb.Append(" | CG mode " + cg.gradingMode.value + " ldrLut " + (cg.ldrLut.value != null ? cg.ldrLut.value.name + " " + cg.ldrLut.value.width + "x" + cg.ldrLut.value.height + " " + cg.ldrLut.value.GetType().Name : "null")
                        + " lift " + cg.lift.value + " gamma " + cg.gamma.value + " gain " + cg.gain.value + " postExp " + cg.postExposure.value + " contrast " + cg.contrast.value + " bright " + cg.brightness.value);
                    Texture lut = cg.ldrLut.value;
                    if (lut != null)
                    {
                        var tmp = RenderTexture.GetTemporary(lut.width, lut.height, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(lut, tmp);
                        RenderTexture prev2 = RenderTexture.active; RenderTexture.active = tmp;
                        var t1 = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                        t1.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                        Color32 l0 = t1.GetPixel(0, 0);
                        sb.Append(" LUT(0,0)=(" + l0.r + "," + l0.g + "," + l0.b + ")");
                        RenderTexture.active = prev2; RenderTexture.ReleaseTemporary(tmp); Object.Destroy(t1);
                    }
                }
                sb.Append(" | ambient " + RenderSettings.ambientLight + " fog " + RenderSettings.fog);
                Plugin.Log.LogInfo(sb.ToString());
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("OLED diag: " + e.Message); }
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
