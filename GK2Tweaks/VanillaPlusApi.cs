using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace GK2Tweaks
{
    // Kleine oeffentliche Schnittstelle fuer die optionale Framework-Anbindung (GK2VanillaPlus.FrameworkBridge.dll).
    // Vanilla+ selbst kennt das Framework nicht; die Bruecke wird nur geladen, wenn es installiert ist.
    public static class VanillaPlusApi
    {
        private static readonly Queue<Action> nextFrame = new Queue<Action>();

        public static string Version => Plugin.PluginVersion;
        public static ConfigFile Config => Plugin.Instance != null ? Plugin.Instance.Config : null;

        // Mod-Menue oeffnen (ueber den Knopf im "Mods"-Menue des Frameworks)
        public static void OpenMenu()
        {
            TweaksGui g = Plugin.Instance != null ? Plugin.Instance.Gui : null;
            if (g != null && !g.MenuOpen) g.ToggleMenu();
        }

        public static string MenuKeyText => Plugin.MenuKey.Value.ToString();

        public static string T(string de, string en) => Labels.T(de, en);

        // Anzeigename und Hilfetext einer Vanilla+-Einstellung in der aktuellen Sprache
        public static string Name(string section, string key)
        {
            ConfigEntryBase e = Find(section, key);
            return e != null ? Labels.Name(e) : key;
        }

        public static string Tip(string section, string key)
        {
            ConfigEntryBase e = Find(section, key);
            return e != null ? Labels.Tip(e) : "";
        }

        // Aktion im naechsten Frame ausfuehren (nicht mitten in einem UI-Klick eines anderen Mods)
        public static void NextFrame(Action a) { if (a != null) nextFrame.Enqueue(a); }

        internal static void Tick()
        {
            int n = nextFrame.Count;
            while (n-- > 0)
            {
                Action a = nextFrame.Dequeue();
                try { a(); } catch (Exception e) { Plugin.Log.LogWarning("NextFrame: " + e.Message); }
            }
        }

        private static ConfigEntryBase Find(string section, string key)
        {
            try
            {
                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in Config)
                    if (kv.Key.Section == section && kv.Key.Key == key) return kv.Value;
            }
            catch { }
            return null;
        }
    }

    // Laedt die Framework-Bruecke nur, wenn GK2 Mod Framework installiert ist (sonst bleibt sie ungenutzt liegen)
    internal static class FrameworkBridgeLoader
    {
        internal const string File = "GK2VanillaPlus.FrameworkBridge.dll";

        // 1.6.1 Hotfix: die Bruecke liess das Spiel zusammen mit GK2 Mod Framework beim Start abstuerzen.
        // Bis das sicher geklaert ist, wird sie nicht mehr geladen (Vanilla+ hat wieder seinen eigenen Mods-Button).
        internal static bool Enabled = false;

        internal static void TryLoad()
        {
            if (!Enabled || !ModsButton.FrameworkInstalled) return;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                string path = System.IO.Path.Combine(dir, File);
                if (!System.IO.File.Exists(path)) { Plugin.Log.LogInfo("GK2 Mod Framework: bridge " + File + " not found, Vanilla+ is not listed in its Mods menu"); return; }
                var asm = System.Reflection.Assembly.LoadFrom(path);
                var m = asm.GetType("GK2VanillaPlus.FrameworkBridge.Bridge")?.GetMethod("Register", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m == null) { Plugin.Log.LogWarning("GK2 Mod Framework: bridge has no Register()"); return; }
                m.Invoke(null, null);
                Plugin.Log.LogInfo("GK2 Mod Framework: Vanilla+ listed in its Mods menu");
            }
            catch (Exception e) { Plugin.Log.LogWarning("GK2 Mod Framework bridge failed: " + (e.InnerException ?? e).Message); }
        }
    }
}
