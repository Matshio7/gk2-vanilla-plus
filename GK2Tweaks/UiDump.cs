#if DEV
using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GK2Tweaks
{
    // Entwicklerhilfe: existiert BepInEx/gk2_uidump.flag, werden im Hauptmenue alle geladenen Sprites/Fonts gelistet und exportiert.
    internal static class UiDump
    {
        private static float since = -1f;
        private static bool done;

        private static int shot;
        private static float shotAt;

        // Entwicklerhilfe 2: BepInEx/gk2_menushot.flag -> im Hauptmenue Mod-Menue oeffnen, Screenshot, beenden.
        private static void ShotTick()
        {
            if (shot < 0) return;
            string flag = Path.Combine(Paths.BepInExRootPath, "gk2_menushot.flag");
            if (shot == 0 && !File.Exists(flag)) { shot = -1; return; }
            if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.MainMenu) return;
            float now = Time.realtimeSinceStartup;
            if (shot == 0) { shot = 1; shotAt = now + 8f; return; }
            if (now < shotAt) return;
            if (shot == 1) { Plugin.Instance.Gui.SetMenu(true); Plugin.Instance.Gui.ScrollToEnd(); shot = 2; shotAt = now + 3f; }
            else if (shot == 2) { ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, "gk2_menushot.png")); shot = 3; shotAt = now + 3f; }
            else if (shot == 3) { File.Delete(flag); shot = -1; Application.Quit(); }
        }

        internal static void Tick()
        {
            ShotTick();
            if (done) return;
            string flag = Path.Combine(Paths.BepInExRootPath, "gk2_uidump.flag");
            if (!File.Exists(flag)) { done = true; return; }
            if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.MainMenu) return;
            if (since < 0f) { since = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - since < 8f) return;
            done = true;
            try { Run(); } catch (Exception e) { Plugin.Log.LogError("UiDump: " + e); }
            File.Delete(flag);
            Application.Quit();
        }

        private static void Run()
        {
            string dir = Path.Combine(Paths.BepInExRootPath, "uidump");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            foreach (Font f in Resources.FindObjectsOfTypeAll<Font>()) sb.AppendLine("FONT " + f.name + " dyn=" + f.dynamic + " size=" + f.fontSize);
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(o => o.GetType().Name == "TMP_FontAsset"))
                sb.AppendLine("TMPFONT " + o.name);
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>().Where(s => s.texture != null).OrderBy(s => s.name).ToArray();
            int n = 0;
            foreach (Sprite s in sprites)
            {
                Rect r = s.textureRect;
                bool sliced = s.border != Vector4.zero;
                sb.AppendLine($"SPRITE {s.name} tex={s.texture.name} {r.width}x{r.height} border={s.border} ppu={s.pixelsPerUnit}");
                string ln = s.name.ToLowerInvariant();
                bool ui = sliced || ln.Contains("window") || ln.Contains("panel") || ln.Contains("button") || ln.Contains("frame") || ln.Contains("bg") || ln.Contains("tooltip") || ln.Contains("slot") || ln.Contains("scroll");
                if (!ui || n >= 400 || r.width * r.height > 1024 * 1024) continue;
                try { Export(s, Path.Combine(dir, n.ToString("000") + "_" + Clean(s.name) + ".png")); n++; } catch { }
            }
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "uidump.txt"), sb.ToString());
            Plugin.Log.LogInfo("UiDump: " + sprites.Length + " Sprites, " + n + " exportiert");
        }

        private static string Clean(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());

        private static void Export(Sprite s, string path)
        {
            Texture2D t = GameSkin.Extract(s);
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }
    }
}

#endif
