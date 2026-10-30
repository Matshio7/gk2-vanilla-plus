using System;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace GK2Tweaks
{
    // Button "Mods" im Hauptmenue und im Pausenmenue (Esc). Kopie des Optionen-Buttons des Spiels,
    // dadurch gleiche Optik und Controller-Navigation. Oeffnet das Mod-Menue; beim Schliessen kommt
    // man wieder ins Spielmenue zurueck.
    internal static class ModsButton
    {
        internal static string Label => "Vanilla+";   // eigener Name, damit es neben dem "Mods"-Button anderer Mods eindeutig ist

        // GK2 Mod Framework bringt einen eigenen "Mods"-Button mit -> dann keinen zweiten anzeigen
        internal const string FrameworkGuid = "ru.superman4eg.gk2.framework";
        private static int framework = -1;
        internal static bool FrameworkInstalled
        {
            get
            {
                if (framework < 0)
                {
                    bool has = false;
                    try { has = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(FrameworkGuid); } catch { }
                    framework = has ? 1 : 0;
                    if (has) Plugin.Log.LogInfo("GK2 Mod Framework detected: both Mods buttons are shown, no bridge is loaded (1.6.1 hotfix)");
                }
                return framework == 1;
            }
        }

        // 1.6.1 Hotfix: wieder ein eigener Button wie vor 1.6.0, auch wenn das Framework installiert ist
        internal static bool Show => Plugin.GameMenuButton.Value;

        internal static void ApplyVisibility()
        {
            try
            {
                foreach (LazyButton b in Resources.FindObjectsOfTypeAll<LazyButton>())
                    if (b != null && b.name == "GK2VanillaPlus_Mods" && b.gameObject.scene.IsValid()) b.gameObject.SetActive(Show);
            }
            catch { }
        }

        // Wirft nie: bei einem Fehler wird ein halb fertiger Klon wieder entfernt (sonst bliebe eine zweite
        // "Optionen"-Taste mit dem Original-Klick stehen).
        internal static LazyButton CloneAfter(LazyButton source, string name, Action onClick)
        {
            if (source == null) return null;
            GameObject go = null;
            try
            {
                Transform parent = source.transform.parent;
                if (parent == null || parent.Find(name) != null) return null;
                // Spiel-Update 1.011: das Spiel hat selbst einen Mods-Button (oft direkt hinter Optionen) ->
                // unseren dahinter setzen statt dazwischen, damit die Reihenfolge/Navigation des Spiels bleibt
                Transform anchor = GameModsButton(source) ?? source.transform;
                go = UnityEngine.Object.Instantiate(source.gameObject, parent);
                go.name = name;
                go.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
                LazyButton b = go.GetComponent<LazyButton>();
                if (b == null) throw new Exception("clone has no LazyButton");
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { try { onClick(); } catch (Exception e) { Plugin.Log.LogWarning("Mods button: " + e.Message); } });
                try { b.SetCallbacksIntoGamepadNavigationItem(); } catch { }
                foreach (LocalizedLabel l in go.GetComponentsInChildren<LocalizedLabel>(true)) l.IgnoreLocalize = true;
                TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
                if (text != null) text.text = Label;
                go.SetActive(Show);
                return b;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Mods button: " + name + " not added: " + e.Message);
                if (go != null) { try { UnityEngine.Object.Destroy(go); } catch { } }
                return null;
            }
        }

        // Mods-Button des Spiels (oder eines anderen Mods) neben dem Optionen-Button: Name oder Text enthaelt "mod"
        // (nicht "mode"/"model"/"modal"/"modif..."). Gesucht wird nur unter denselben Geschwistern, damit die Position
        // im selben Layout bleibt. Der letzte Treffer gewinnt, unser eigener Button zaehlt nicht.
        private static bool loggedGameButton;
        private static Transform GameModsButton(LazyButton source)
        {
            try
            {
                Transform parent = source.transform.parent;
                Transform hit = null;
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform c = parent.GetChild(i);
                    if (c == null || c == source.transform || c.name.StartsWith("GK2VanillaPlus_", StringComparison.Ordinal)) continue;
                    if (c.GetComponent<LazyButton>() == null) continue;
                    string label = "";
                    try { TMP_Text t = c.GetComponentInChildren<TMP_Text>(true); if (t != null) label = t.text ?? ""; } catch { }
                    if (IsModsName(c.name) || IsModsName(label)) hit = c;
                }
                if (hit != null && !loggedGameButton)
                {
                    loggedGameButton = true;
                    Plugin.Log.LogInfo("Mods button: found existing mods button '" + hit.name + "' in " + parent.name + " - Vanilla+ button goes after it");
                }
                return hit;
            }
            catch { return null; }
        }

        private static bool IsModsName(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string l = s.ToLowerInvariant();
            int i = l.IndexOf("mod", StringComparison.Ordinal);
            while (i >= 0)
            {
                string rest = l.Substring(i);
                if (!rest.StartsWith("mode", StringComparison.Ordinal) && !rest.StartsWith("modal", StringComparison.Ordinal)
                    && !rest.StartsWith("model", StringComparison.Ordinal) && !rest.StartsWith("modif", StringComparison.Ordinal)
                    && !rest.StartsWith("modul", StringComparison.Ordinal))
                    return true;
                i = l.IndexOf("mod", i + 3, StringComparison.Ordinal);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UIMainMenuWindow), nameof(UIMainMenuWindow.Init))]
    internal static class MainMenuModsButtonPatch
    {
        private static void Postfix(UIMainMenuWindow __instance)
        {
            try
            {
                LazyButton settings = Traverse.Create(__instance).Field("gameSettingsButton").GetValue<LazyButton>();
                ModsButton.CloneAfter(settings, "GK2VanillaPlus_Mods", () =>
                {
                    __instance.Close();
                    Plugin.Instance.Gui.OpenFromGameMenu(() => { if (MainGame.Instance.gameState == MainGame.GameState.MainMenu) __instance.Open(null); });
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning("Mods button (main menu): " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UIGamePauseWindow), nameof(UIGamePauseWindow.Init))]
    internal static class PauseModsButtonPatch
    {
        private static void Postfix(UIGamePauseWindow __instance)
        {
            try
            {
                LazyButton settings = Traverse.Create(__instance).Field("settingsBtn").GetValue<LazyButton>();
                ModsButton.CloneAfter(settings, "GK2VanillaPlus_Mods", () =>
                {
                    __instance.Close();
                    Plugin.Instance.Gui.OpenFromGameMenu(() => { if (MainGame.Instance.gameState == MainGame.GameState.InGame) __instance.Open(null); });
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning("Mods button (pause menu): " + e.Message); }
        }
    }
}
