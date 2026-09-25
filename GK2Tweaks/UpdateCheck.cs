using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;

namespace GK2Tweaks
{
    // Update-Pruefung gegen GitHub-Releases (einmal pro Spielstart, abschaltbar).
    // Windows: "Speichern & aktualisieren" startet den mitgelieferten Updater, der nach dem Beenden des Spiels
    // die neue Version herunterlaedt und installiert. Mac/Linux: Button oeffnet die Download-Seite.
    internal static class UpdateCheck
    {
        internal const string Repo = "Matshio7/gk2-vanilla-plus";
        internal const string ReleasePage = "https://github.com/" + Repo + "/releases/latest";
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";

        internal static bool Available;
        internal static string Latest = "";
        private static bool started, updating;

        internal static string UpdaterPath => Path.Combine(Path.Combine(Paths.GameRootPath, "BepInEx"), Path.Combine("GK2VanillaPlus", "update.ps1"));
        internal static bool CanAutoUpdate => !WineFix.IsWine && File.Exists(UpdaterPath);

        internal static IEnumerator Run()
        {
            if (started || !Plugin.CheckUpdates.Value) yield break;
            started = true;
            yield return new WaitForSecondsRealtime(8f);
            using (UnityWebRequest req = UnityWebRequest.Get(Api))
            {
                req.SetRequestHeader("User-Agent", "GK2VanillaPlus/" + Plugin.PluginVersion);
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogInfo("Update check: " + req.error);
                    yield break;
                }
                Match m = Regex.Match(req.downloadHandler.text, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})\"");
                if (!m.Success) yield break;
                try
                {
                    if (new Version(m.Groups[1].Value) > new Version(Plugin.PluginVersion))
                    {
                        Latest = m.Groups[1].Value;
                        Available = true;
                        Plugin.Log.LogInfo("Update available: " + Latest);
                        if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.InGame)
                            ManualSave.Toast(Labels.T("GK2 Vanilla+ " + Latest + " ist verfügbar – F9 zum Aktualisieren", "GK2 Vanilla+ " + Latest + " is available – press F9 to update"), 8f);
                    }
                    else Plugin.Log.LogInfo("Update check: up to date (" + m.Groups[1].Value + ")");
                }
                catch (Exception e) { Plugin.Log.LogInfo("Update check: " + e.Message); }
            }
        }

        // Im Spiel erst speichern, dann Updater starten und das Spiel beenden.
        internal static void SaveAndUpdate(bool menuOpen)
        {
            if (updating) return;
            MainGame mg = MainGame.Instance;
            if (mg != null && mg.gameState == MainGame.GameState.InGame) ManualSave.Save(menuOpen, StartUpdater);
            else StartUpdater();
        }

        private static void StartUpdater()
        {
            try
            {
                updating = true;
                string args = "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + UpdaterPath + "\" -GameDir \"" +
                              Paths.GameRootPath.TrimEnd('\\', '/') + "\" -WaitPid " + Process.GetCurrentProcess().Id;
                Process.Start(new ProcessStartInfo("powershell.exe", args) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                Plugin.Log.LogInfo("Updater started, quitting game");
                Application.Quit();
            }
            catch (Exception e)
            {
                updating = false;
                Plugin.Log.LogWarning("Updater: " + e.Message);
                ManualSave.Toast(Labels.T("Updater konnte nicht gestartet werden – Download-Seite wird geöffnet.", "Could not start the updater – opening the download page."), 5f);
                Application.OpenURL(ReleasePage);
            }
        }
    }
}
