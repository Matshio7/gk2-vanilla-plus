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
        internal static string Label => Labels.T("Mods", "Mods");

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
                    if (has) Plugin.Log.LogInfo("GK2 Mod Framework detected: its Mods button is used, Vanilla+ adds no second one (menu: F9)");
                }
                return framework == 1;
            }
        }

        internal static bool Show => Plugin.GameMenuButton.Value && !FrameworkInstalled;

        internal static void ApplyVisibility()
        {
            try
            {
                foreach (LazyButton b in Resources.FindObjectsOfTypeAll<LazyButton>())
                    if (b != null && b.name == "GK2VanillaPlus_Mods" && b.gameObject.scene.IsValid()) b.gameObject.SetActive(Show);
            }
            catch { }
        }

        internal static LazyButton CloneAfter(LazyButton source, string name, Action onClick)
        {
            if (source == null) return null;
            Transform parent = source.transform.parent;
            if (parent.Find(name) != null) return null;
            GameObject go = UnityEngine.Object.Instantiate(source.gameObject, parent);
            go.name = name;
            go.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            LazyButton b = go.GetComponent<LazyButton>();
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { try { onClick(); } catch (Exception e) { Plugin.Log.LogWarning("Mods button: " + e.Message); } });
            try { b.SetCallbacksIntoGamepadNavigationItem(); } catch { }
            foreach (LocalizedLabel l in go.GetComponentsInChildren<LocalizedLabel>(true)) l.IgnoreLocalize = true;
            TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = Label;
            go.SetActive(Show);
            return b;
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
