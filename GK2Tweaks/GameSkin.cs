using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    // Holt Rahmen/Buttons/Schrift aus den geladenen Spiel-Sprites, damit Mod-Menue und FPS-Anzeige wie das Spiel aussehen.
    internal static class GameSkin
    {
        internal const int K = 3; // Pixel-Art-Vergroesserung (bei 1080p)
        internal static bool Loaded;
        internal static Font PixelFont;
        private static float nextTry;
        private static readonly Dictionary<string, KeyValuePair<Texture2D, RectOffset>> cache = new Dictionary<string, KeyValuePair<Texture2D, RectOffset>>();

        internal static bool TryLoad()
        {
            if (Loaded) return true;
            if (Time.realtimeSinceStartup < nextTry) return false;
            nextTry = Time.realtimeSinceStartup + 2f;
            try
            {
                var byName = new Dictionary<string, Sprite>();
                foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s == null || s.texture == null) continue;
                    if (!byName.TryGetValue(s.name, out Sprite old) || (old.border == Vector4.zero && s.border != Vector4.zero)) byName[s.name] = s;
                }
                string[] need = { "body_table-bg_1", "comm-frame_1-border", "main_window-header_1", "comm-btn-simple_red-active",
                    "comm-btn-simple_red-over", "comm-btn-simple_red-press", "comm-btn-small_grey-active", "comm-btn-small_grey-over",
                    "comm-btn-small_grey-press", "comm-value_frame_2", "craft_window-craft_plate", "hint-frame" };
                foreach (string n in need)
                {
                    if (!byName.TryGetValue(n, out Sprite s)) { Plugin.Log.LogDebug("GameSkin: Sprite fehlt " + n); return false; }
                    Texture2D raw = Extract(s);
                    if (n.Contains("btn") || n.Contains("value_frame")) raw = Trim(raw);
                    cache[n] = new KeyValuePair<Texture2D, RectOffset>(Upscale(raw), new RectOffset(
                        Mathf.RoundToInt(s.border.x) * K, Mathf.RoundToInt(s.border.z) * K, Mathf.RoundToInt(s.border.w) * K, Mathf.RoundToInt(s.border.y) * K));
                }
                PixelFont = FindFont();
                Loaded = true;
                Plugin.Log.LogInfo("GameSkin geladen, Schrift: " + (PixelFont != null ? PixelFont.name : "Standard"));
            }
            catch (Exception e) { Plugin.Log.LogWarning("GameSkin: " + e.Message); nextTry = float.MaxValue; }
            return Loaded;
        }

        private static Font FindFont()
        {
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if (o == null || o.GetType().Name != "TMP_FontAsset" || o.name != "small_font") continue;
                Font f = null;
                try { f = Traverse.Create(o).Property("sourceFontFile").GetValue<Font>(); } catch { }
                if (f == null) try { f = Traverse.Create(o).Field("m_SourceFontFile").GetValue<Font>(); } catch { }
                if (f != null) { Plugin.Log.LogInfo("GameSkin: Font " + f.name + " dyn=" + f.dynamic + " size=" + f.fontSize); return f; }
            }
            return null;
        }

        // Transparente Raender (Schatten-/Abstandspixel im Sprite) abschneiden
        private static Texture2D Trim(Texture2D src)
        {
            int w = src.width, h = src.height, x0 = w, y0 = h, x1 = -1, y1 = -1;
            Color32[] a = src.GetPixels32();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (a[y * w + x].a > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0 || (x0 == 0 && y0 == 0 && x1 == w - 1 && y1 == h - 1)) return src;
            var t = new Texture2D(x1 - x0 + 1, y1 - y0 + 1, TextureFormat.RGBA32, false);
            t.SetPixels(src.GetPixels(x0, y0, t.width, t.height));
            t.Apply();
            UnityEngine.Object.Destroy(src);
            return t;
        }

        internal static Texture2D Extract(Sprite s)
        {
            Rect r = s.textureRect;
            var rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(s.texture, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        private static Texture2D Upscale(Texture2D src)
        {
            int w = src.width, h = src.height;
            Color32[] a = src.GetPixels32();
            var b = new Color32[w * K * h * K];
            for (int y = 0; y < h * K; y++)
                for (int x = 0; x < w * K; x++)
                    b[y * w * K + x] = a[(y / K) * w + x / K];
            var t = new Texture2D(w * K, h * K, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels32(b);
            t.Apply();
            UnityEngine.Object.Destroy(src);
            return t;
        }

        internal static GUIStyle Box(string sprite, GUIStyle baseStyle, int fontSize, Color text)
        {
            var kv = cache[sprite];
            var st = new GUIStyle(baseStyle) { border = kv.Value, fontSize = fontSize };
            st.normal.background = kv.Key;
            st.normal.textColor = text;
            if (PixelFont != null) st.font = PixelFont;
            return st;
        }

        internal static GUIStyle Button(string baseName, GUIStyle baseStyle, int fontSize, Color text)
        {
            GUIStyle st = Box(baseName + "-active", baseStyle, fontSize, text);
            st.hover.background = cache[baseName + "-over"].Key; st.hover.textColor = Color.white;
            st.active.background = cache[baseName + "-press"].Key; st.active.textColor = Color.white;
            st.onNormal = st.normal; st.onHover = st.hover; st.onActive = st.active;
            st.focused = st.normal;
            return st;
        }

        internal static Texture2D Tex(string sprite) => cache[sprite].Key;
    }
}
