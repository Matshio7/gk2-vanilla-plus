#if !NEXUS
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
    // Update-Pruefung gegen GitHub-Releases (Kanal Stabil oder Beta) (einmal pro Spielstart, abschaltbar).
    // Windows: "Speichern & aktualisieren" startet den mitgelieferten Updater, der nach dem Beenden des Spiels
    // die neue Version herunterlaedt und installiert. Mac/Linux: Button oeffnet die Download-Seite.
    internal static class UpdateCheck
    {
        internal const string Repo = "Matshio7/gk2-vanilla-plus";
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases";

        internal static bool Beta => Plugin.UpdateChannel != null && Plugin.UpdateChannel.Value == "Beta";
        internal static string ReleasePage => "https://github.com/" + Repo + (Beta ? "/releases" : "/releases/latest");
        internal static string Channel => Beta ? "beta" : "stable";

        internal static bool Available;
        internal static string Latest = "";
        private static bool started, updating;

        internal static string UpdaterPath => Path.Combine(Path.Combine(Paths.GameRootPath, "BepInEx"), Path.Combine("GK2VanillaPlus", "update.ps1"));
        internal static bool CanAutoUpdate => !WineFix.IsWine && File.Exists(UpdaterPath);

        // Versionsschluessel: 4. Stelle 65534 = stabil, sonst Beta-Nummer (1.8.0-beta.2 < 1.8.0)
        private static Version Key(string numbers, string beta)
        {
            Version v = new Version(numbers);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), string.IsNullOrEmpty(beta) ? 65534 : int.Parse(beta));
        }

        private static string Show(Version k) => k.Revision == 65534 ? k.Major + "." + k.Minor + "." + k.Build : k.Major + "." + k.Minor + "." + k.Build + " Beta " + k.Revision;

        internal static IEnumerator Run()
        {
            if (started || !Plugin.CheckUpdates.Value) yield break;
            started = true;
            yield return new WaitForSecondsRealtime(8f);
            bool beta = Beta;
            using (UnityWebRequest req = UnityWebRequest.Get(beta ? Api + "?per_page=20" : Api + "/latest"))
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
                try
                {
                    // stabil: nur das neueste Release; Beta: das hoechste aus der Liste (auch Pre-releases)
                    Version best = null;
                    foreach (Match m in Regex.Matches(req.downloadHandler.text, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})(?:-beta\\.?([0-9]+))?\""))
                    {
                        Version k = Key(m.Groups[1].Value, m.Groups[2].Value);
                        if (best == null || k > best) best = k;
                        if (!beta) break;
                    }
                    Version mine = Key(Plugin.PluginVersion, Plugin.BetaNumber > 0 ? Plugin.BetaNumber.ToString() : "");
                    if (best == null) { }
                    else if (best > mine)
                    {
                        Latest = Show(best);
                        Available = true;
                        Plugin.Log.LogInfo("Update available: " + Latest);
                        if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.InGame)
                            ManualSave.Toast(string.Format(Labels.T("GK2 Vanilla+ {0} ist verfügbar – F9 zum Aktualisieren", "GK2 Vanilla+ {0} is available – press F9 to update"), Latest), 8f);
                    }
                    else Plugin.Log.LogInfo("Update check: up to date (" + Show(best) + ", channel " + Channel + ")");
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
                              Paths.GameRootPath.TrimEnd('\\', '/') + "\" -Channel " + Channel + " -WaitPid " + Process.GetCurrentProcess().Id;
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
#else
using System.Collections;

namespace GK2Tweaks
{
    // Nexus-Ausgabe: keine Update-Pruefung, keine Internetverbindung, kein Updater. Updates kommen ueber Nexus Mods.
    internal static class UpdateCheck
    {
        internal const string ReleasePage = "";
        internal static bool Available => false;
        internal static string Latest => "";
        internal static bool CanAutoUpdate => false;
        internal static IEnumerator Run() { yield break; }
        internal static void SaveAndUpdate(bool menuOpen) { }
    }
}
#endif
