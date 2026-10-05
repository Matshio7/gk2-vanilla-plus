using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace GK2Tweaks
{
    // Hauptmenue: alternative Hintergruende. "Default" = die animierte Szene des Spiels. Sonst ein Bild:
    // drei Motive von uns (BepInEx/GK2VanillaPlus/MenuBackground/bg1-3.jpg) oder ein eigenes Bild aus dem Spielstand ("Aktuelle Ansicht als
    // Menü-Hintergrund" im Mod-Menue, ohne HUD aufgenommen). Darueber Filter-Ebenen: Stil (Farbe), Weichzeichnen,
    // Vignette und Abdunkeln, damit das Menue lesbar bleibt. Berechnet wird einmal beim Laden, im Menue kostet es nichts.
    internal sealed class MenuBackground : MonoBehaviour
    {
        internal static string Folder => Path.Combine(Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus"), "MenuBackground");
        internal static string MinePath => Path.Combine(Folder, "mine.png");

        private static Texture2D styled;
        private static string styledKey;
        private static float retryAt;

        internal static bool HasMine => File.Exists(MinePath);

        // ImageConversion per Reflection (das Modul verlangt sonst netstandard 2.1 beim Kompilieren)
        private static readonly Type IC = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        private static bool LoadImg(Texture2D t, byte[] b)
        {
            MethodInfo mi = IC?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            return mi != null && (bool)mi.Invoke(null, new object[] { t, b });
        }
        private static byte[] Png(Texture2D t) => IC?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) })?.Invoke(null, new object[] { t }) as byte[];

        // aktiv = Hauptmenue + Bild gewaehlt
        internal static bool Active
        {
            get
            {
                try
                {
                    string m = Plugin.MenuBg.Value;
                    if (m == "Scene" || !SafeMode.On("MenuBackground")) return false;
                    if (m == "Mine" && !HasMine) return false;
                    return MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.MainMenu;
                }
                catch { return false; }
            }
        }

        internal static void Tick()
        {
            try
            {
                Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : null;
                if (cam == null) return;
                MenuBackground b = cam.GetComponent<MenuBackground>();
                bool want = Active;
                if (b == null) { if (!want) return; b = cam.gameObject.AddComponent<MenuBackground>(); }
                if (b.enabled != want) b.enabled = want;
                if (!want && styled != null && MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.InGame) Drop();
            }
            catch (Exception e) { SafeMode.Fail("MenuBackground", e); }
        }

        private static void Drop()
        {
            if (styled != null) Destroy(styled);
            styled = null; styledKey = null;
        }

        private static string Key => Plugin.MenuBg.Value + "|" + Plugin.MenuBgStyle.Value + "|" + Plugin.MenuBgBlur.Value + "|" + Plugin.MenuBgDim.Value + "|" + (HasMine ? File.GetLastWriteTimeUtc(MinePath).Ticks : 0);

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Texture2D t = null;
            try
            {
                string k = Key;
                if (styledKey != k && Time.realtimeSinceStartup >= retryAt) { Drop(); styled = Build(); styledKey = k; if (styled == null) retryAt = Time.realtimeSinceStartup + 5f; }
                t = styled;
            }
            catch (Exception e) { SafeMode.Fail("MenuBackground", e); }
            if (t == null) { Graphics.Blit(src, dst); return; }
            // "cover": Bild fuellt den ganzen Bildschirm, ueberstehender Teil wird abgeschnitten
            float sa = (float)src.width / Mathf.Max(1, src.height), ta = (float)t.width / Mathf.Max(1, t.height);
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            if (sa > ta) { scale.y = ta / sa; offset.y = (1f - scale.y) / 2f; }
            else { scale.x = sa / ta; offset.x = (1f - scale.x) / 2f; }
            Graphics.Blit(t, dst, scale, offset);
        }

        private void OnDestroy() { }

        // ---------- Bild laden + Filter ----------
        private static Texture2D LoadSource()
        {
            byte[] bytes = null;
            string m = Plugin.MenuBg.Value;
            if (m == "Mine") { if (HasMine) bytes = File.ReadAllBytes(MinePath); }
            else
            {
                // Motive liegen als bg1.jpg .. bg3.jpg neben mine.png (werden mit der Mod installiert)
                string p = Path.Combine(Folder, m.ToLowerInvariant() + ".jpg");
                if (File.Exists(p)) bytes = File.ReadAllBytes(p);
            }
            if (bytes == null) { Plugin.Log.LogWarning("Menu background: no image for " + m); return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!LoadImg(tex, bytes)) { Destroy(tex); return null; }
            return tex;
        }

        private static Texture2D Build()
        {
            Texture2D src = LoadSource();
            if (src == null) return null;
            // auf hoechstens 1920 Breite bringen und weichzeichnen (Kaskade runter und wieder hoch, wie MenuSideFill)
            // Zielbreite passend zum Bildschirm (cover auf 16:9-Bild), hoechstens 4K
            // Zielbreite: so breit, dass das Bild den Bildschirm fuellt (Motive sind 32:9, auf 16:9 wird die Mitte genutzt)
            float srcAspect = (float)src.width / Mathf.Max(1, src.height);
            int target = Mathf.Max(Screen.width, Mathf.CeilToInt(Screen.height * srcAspect));
            target = Mathf.Clamp(target, 1280, 8192);
            int W = Mathf.Min(target, src.width), H = Mathf.Max(1, Mathf.RoundToInt(W * (float)src.height / src.width));
            string style = Plugin.MenuBgStyle.Value;
            int blur = Mathf.Clamp(Plugin.MenuBgBlur.Value, 0, 3);
            if (style == "Painting") blur = Mathf.Max(blur, 1);
            RenderTexture prev = RenderTexture.active;
            RenderTexture full = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
            full.filterMode = FilterMode.Bilinear;
            src.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, full);
            Destroy(src);
            if (blur > 0)
            {
                int steps = blur + 1; // 1 -> /4, 2 -> /8, 3 -> /16
                var chain = new RenderTexture[steps];
                RenderTexture cur = full;
                for (int i = 0; i < steps; i++)
                {
                    chain[i] = RenderTexture.GetTemporary(Mathf.Max(4, W >> (i + 1)), Mathf.Max(4, H >> (i + 1)), 0, RenderTextureFormat.ARGB32);
                    chain[i].filterMode = FilterMode.Bilinear;
                    Graphics.Blit(cur, chain[i]); cur = chain[i];
                }
                for (int i = steps - 2; i >= 0; i--) { Graphics.Blit(cur, chain[i]); cur = chain[i]; }
                Graphics.Blit(cur, full);
                foreach (var c in chain) RenderTexture.ReleaseTemporary(c);
            }
            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            RenderTexture.active = full;
            outTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(full);
            Color32[] px = outTex.GetPixels32();
            Grade(px, W, H, style, Plugin.MenuBgDim.Value / 100f);
            outTex.SetPixels32(px);
            outTex.Apply(false, false);
            Plugin.Log.LogInfo("Menu background: " + Plugin.MenuBg.Value + " " + style + " blur " + blur + " " + W + "x" + H);
            return outTex;
        }

        // Farb-Stil + Vignette + Abdunkeln (pro Pixel)
        private static void Grade(Color32[] px, int W, int H, string style, float dim)
        {
            float cx = W / 2f, cy = H / 2f, inv = 1f / Mathf.Sqrt(cx * cx + cy * cy);
            float bright = 1f - Mathf.Clamp01(dim);
            for (int y = 0; y < H; y++)
            {
                float dy = (y - cy);
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color32 c = px[i];
                    float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                    float l = 0.299f * r + 0.587f * g + 0.114f * b;
                    switch (style)
                    {
                        case "Gloomy":
                            // entsaettigt, kuehle Schatten, etwas mehr Kontrast
                            r = Mathf.Lerp(l, r, 0.55f); g = Mathf.Lerp(l, g, 0.55f); b = Mathf.Lerp(l, b, 0.55f);
                            r = (r - 0.5f) * 1.15f + 0.5f; g = (g - 0.5f) * 1.15f + 0.5f; b = (b - 0.5f) * 1.15f + 0.5f;
                            { float s = 1f - l; r = r * 0.92f + 0.02f * s; g = g * 0.95f + 0.05f * s; b = b + 0.07f * s; }
                            break;
                        case "Sepia":
                            {
                                float sr = r * 0.393f + g * 0.769f + b * 0.189f, sg = r * 0.349f + g * 0.686f + b * 0.168f, sb = r * 0.272f + g * 0.534f + b * 0.131f;
                                r = Mathf.Lerp(r, sr, 0.85f); g = Mathf.Lerp(g, sg, 0.85f); b = Mathf.Lerp(b, sb, 0.85f);
                            }
                            break;
                        case "Night":
                            r = Mathf.Lerp(l, r, 0.45f) * 0.55f; g = Mathf.Lerp(l, g, 0.45f) * 0.72f; b = Mathf.Lerp(l, b, 0.45f) * 1.05f + 0.03f;
                            break;
                        case "Painting":
                            {
                                // kraeftigere Farben + wenige Tonstufen = gemalter Look
                                r = Mathf.Lerp(l, r, 1.25f); g = Mathf.Lerp(l, g, 1.25f); b = Mathf.Lerp(l, b, 1.25f);
                                const float lv = 7f;
                                r = Mathf.Round(Mathf.Clamp01(r) * lv) / lv; g = Mathf.Round(Mathf.Clamp01(g) * lv) / lv; b = Mathf.Round(Mathf.Clamp01(b) * lv) / lv;
                            }
                            break;
                    }
                    float dx = x - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * inv;              // 0 Mitte .. 1 Ecke
                    float v = 1f - 0.6f * Mathf.SmoothStep(0.35f, 1f, d);       // Vignette
                    float k = v * bright;
                    px[i] = new Color32((byte)(Mathf.Clamp01(r * k) * 255f), (byte)(Mathf.Clamp01(g * k) * 255f), (byte)(Mathf.Clamp01(b * k) * 255f), 255);
                }
            }
        }

        // ---------- eigenes Bild aufnehmen (im Spiel, ohne HUD und Mod-Anzeigen) ----------
        internal static bool Capturing { get; private set; }

        internal static void CaptureMine()
        {
            if (Capturing || !WeekPlan.InGame) return;
            Plugin.Instance.StartCoroutine(CaptureRoutine(MinePath, true, 0));
        }

        internal static System.Collections.IEnumerator CaptureRoutine(string path, bool toast, int superSize)
        {
            Capturing = true;
            try { Plugin.Instance.Gui.SetMenu(false); } catch { }
            bool hid = false;
            if (!HudToggle.Hidden && WeekPlan.InGame) { HudToggle.Toggle(); hid = HudToggle.Hidden; }
            yield return null; yield return null; yield return null;
            yield return new WaitForEndOfFrame();
            bool ok = false;
            try
            {
                // hoehere Aufloesung als der Bildschirm (fuer scharfe Bilder auf 4K/Ultrawide), max. 16384 px Kante
                int ss = superSize > 0 ? superSize : Mathf.Clamp(Mathf.CeilToInt(3840f / Mathf.Max(1, Screen.width)), 1, 3);
                while (ss > 1 && (Screen.width * ss > 16384 || Screen.height * ss > 16384)) ss--;
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture(ss);
                if (shot != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    byte[] png = Png(shot);
                    if (png == null) throw new Exception("PNG encode failed");
                    File.WriteAllBytes(path, png);
                    Destroy(shot);
                    ok = true;
                    Plugin.Log.LogInfo("Menu background captured: " + path);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Menu background capture: " + e.Message); }
            yield return null;
            if (hid) HudToggle.Show();
            Capturing = false;
            if (ok && path == MinePath) { styledKey = null; if (Plugin.MenuBg.Value != "Mine") Plugin.MenuBg.Value = "Mine"; }
            if (toast) ManualSave.Toast(ok ? Labels.T("Menü-Hintergrund gespeichert – zu sehen im Hauptmenü.", "Menu background saved – see it in the main menu.")
                                           : Labels.T("Aufnahme fehlgeschlagen.", "Capture failed."), 3f);
        }
    }
}
