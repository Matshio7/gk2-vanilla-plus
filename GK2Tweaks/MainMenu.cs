using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    // Hauptmenue auf Ultrawide: die Seiten werden mit einer unscharfen, abgedunkelten Spiegelung des Menuebilds gefuellt.
    internal sealed class MenuSideFill : MonoBehaviour
    {
        private RenderTexture[] chain;
        private Texture2D grad;

        internal static void Tick()
        {
            try
            {
                Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : null;
                if (cam == null) return;
                MenuSideFill f = cam.GetComponent<MenuSideFill>();
                bool want = Plugin.MenuExtend.Value && MainGame.Instance != null
                    && MainGame.Instance.gameState == MainGame.GameState.MainMenu
                    && (float)Screen.width / Screen.height > 16f / 9f + 0.05f;
                if (f == null) { if (!want) return; f = cam.gameObject.AddComponent<MenuSideFill>(); }
                if (f.enabled != want) f.enabled = want;
            }
            catch { }
        }

        // Kaskade: halbieren bis 1/32, dann zweimal wieder hoch -> weicher Blur ohne Pixeltreppen
        private static readonly int[] Div = { 2, 4, 8, 16, 32, 16, 8 };

        private void EnsureChain(int W, int H)
        {
            if (chain != null && chain[0].width == Mathf.Max(4, W / 2) && chain[0].height == Mathf.Max(4, H / 2)) return;
            Release();
            chain = new RenderTexture[Div.Length];
            for (int i = 0; i < Div.Length; i++)
                chain[i] = new RenderTexture(Mathf.Max(4, W / Div[i]), Mathf.Max(4, H / Div[i]), 0) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (grad == null)
            {
                // Alpha-Verlauf: aussen 0.35, zur Bildkante hin 0.85 (weicher Schatten an der Naht)
                grad = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < 128; x++)
                {
                    float t = x / 127f;
                    float a = 0.35f + 0.5f * Mathf.Pow(Mathf.SmoothStep(0.55f, 1f, t), 1.5f);
                    grad.SetPixel(x, 0, new Color(0, 0, 0, a));
                }
                grad.Apply();
            }
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Graphics.Blit(src, dst);
            int W = src.width, H = src.height;
            float imgW = H * 16f / 9f;
            float x0 = (W - imgW) / 2f;
            if (x0 < 4f) return;
            EnsureChain(W, H);
            Graphics.Blit(src, chain[0]);
            for (int i = 1; i < chain.Length; i++) Graphics.Blit(chain[i - 1], chain[i]);
            RenderTexture blur = chain[chain.Length - 1];

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = dst;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, W, H, 0);
            float u0 = x0 / W, uw = imgW / W * 0.4f;
            var tint = new Color(0.36f, 0.36f, 0.4f, 0.5f);
            // gespiegelt: die Bildkante setzt sich nach aussen fort
            Graphics.DrawTexture(new Rect(0, 0, x0, H), blur, new Rect(u0 + uw, 0, -uw, 1), 0, 0, 0, 0, tint);
            Graphics.DrawTexture(new Rect(W - x0, 0, x0, H), blur, new Rect(1 - u0, 0, -uw, 1), 0, 0, 0, 0, tint);
            Graphics.DrawTexture(new Rect(0, 0, x0, H), grad);
            Graphics.DrawTexture(new Rect(W - x0, 0, x0, H), grad, new Rect(1, 0, -1, 1), 0, 0, 0, 0);
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        private void Release()
        {
            if (chain == null) return;
            foreach (RenderTexture rt in chain) if (rt != null) rt.Release();
            chain = null;
        }

        private void OnDisable() => Release();
    }

    // Hauptmenue: Hinweis "modded" unter der Versionsnummer.
    [HarmonyPatch(typeof(UIMainMenuInfoPanel), "Draw")]
    internal static class ModdedLabelPatch
    {
        private static void Postfix(UIMainMenuInfoPanel __instance)
        {
            if (!Plugin.MenuModdedLabel.Value) return;
            Traverse text = Traverse.Create(__instance).Field("versionLabel").Property("text");
            string t = text.GetValue<string>();
            if (t == null || t.Contains("modded")) return;
            string add = "  <color=#8fd18f>· modded · GK2 Vanilla+ by McFly7</color>";
            if (UpdateCheck.Available) add += "  <color=#ffd27f>· Update " + UpdateCheck.Latest + " (F9)</color>";
            text.SetValue(t + add);
        }
    }
}
