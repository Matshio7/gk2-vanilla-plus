using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Steamworks;

namespace GK2Tweaks
{
    // Der Workshop-Uploader des Spiels (Shift+F11) setzt bei jedem Upload die Beschreibung auf den Titel,
    // stellt die Sichtbarkeit auf "Nicht gelistet" und schreibt als Aenderungshinweis nur "Updated".
    // Fuer GK2 Vanilla+ selbst korrigieren wir das: Beschreibung aus BepInEx/GK2VanillaPlus/workshop_description.bbcode,
    // Sichtbarkeit bleibt wie auf Steam eingestellt, Aenderungshinweis = Changelog-Eintrag der hochgeladenen Version.
    // Greift nur, wenn diese Datei existiert (also nur beim Autor) und der Titel mit "GK2 Vanilla+" beginnt.
    internal static class WorkshopUpload
    {
        internal static string DescriptionFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus", "workshop_description.bbcode");
        private static string title, contentFolder;

        internal static bool Active => title != null && title.StartsWith("GK2 Vanilla+") && File.Exists(DescriptionFile);

        internal static void Apply(Harmony h)
        {
            try
            {
                h.Patch(AccessTools.Method(typeof(SteamUGC), nameof(SteamUGC.SetItemTitle)), prefix: new HarmonyMethod(typeof(WorkshopUpload), nameof(TitlePrefix)));
                h.Patch(AccessTools.Method(typeof(SteamUGC), nameof(SteamUGC.SetItemContent)), prefix: new HarmonyMethod(typeof(WorkshopUpload), nameof(ContentPrefix)));
                h.Patch(AccessTools.Method(typeof(SteamUGC), nameof(SteamUGC.SetItemVisibility)), prefix: new HarmonyMethod(typeof(WorkshopUpload), nameof(VisibilityPrefix)));
                h.Patch(AccessTools.Method(typeof(SteamUGC), nameof(SteamUGC.SubmitItemUpdate)), prefix: new HarmonyMethod(typeof(WorkshopUpload), nameof(SubmitPrefix)));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Workshop upload patch: " + e.Message); }
        }

        private static void TitlePrefix(string pchTitle) { title = pchTitle; contentFolder = null; }

        private static void ContentPrefix(string pszContentFolder) { contentFolder = pszContentFolder; }

        private static bool VisibilityPrefix(ref bool __result)
        {
            if (!Active) return true;
            Plugin.Log.LogInfo("Workshop upload: visibility left unchanged");
            __result = true;
            return false;
        }

        private static void SubmitPrefix(UGCUpdateHandle_t handle, ref string pchChangeNote)
        {
            try
            {
                if (!Active) return;
                string desc = File.ReadAllText(DescriptionFile, Encoding.UTF8).Replace("\r\n", "\n");
                bool ok = SteamUGC.SetItemDescription(handle, desc);
                Plugin.Log.LogInfo("Workshop upload: description from file (" + desc.Length + " chars) " + (ok ? "set" : "FAILED"));
                string note = ChangeNote(contentFolder);
                if (!string.IsNullOrEmpty(note)) { pchChangeNote = note; Plugin.Log.LogInfo("Workshop upload: change note set"); }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Workshop upload: " + e.Message); }
        }

        // Neuester Eintrag aus docs/CHANGELOG.md im hochgeladenen Ordner, als Steam-BBCode
        internal static string ChangeNote(string folder)
        {
            string path = folder != null ? Path.Combine(folder, "docs", "CHANGELOG.md") : null;
            string md = path != null && File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            if (md == null) return null;
            var sb = new StringBuilder();
            bool inEntry = false, inList = false;
            foreach (string raw in md.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                Match m = Regex.Match(line, @"^##\s+(\d+\.\d+\.\d+)(.*)$");
                if (m.Success)
                {
                    if (inEntry) break;
                    inEntry = true;
                    sb.Append("[b]").Append(m.Groups[1].Value).Append("[/b]").Append(m.Groups[2].Value).Append('\n');
                    continue;
                }
                if (!inEntry || line.Length == 0) continue;
                if (line == "**EN**" || line == "**DE**")
                {
                    if (inList) { sb.Append("[/list]\n"); inList = false; }
                    if (line == "**DE**") sb.Append("\n[b]Deutsch[/b]\n");
                    continue;
                }
                string text = Regex.Replace(line, @"\*\*(.+?)\*\*", "[b]$1[/b]");
                if (text.StartsWith("- "))
                {
                    if (!inList) { sb.Append("[list]\n"); inList = true; }
                    sb.Append("[*]").Append(text.Substring(2)).Append('\n');
                }
                else sb.Append(text).Append('\n');
            }
            if (inList) sb.Append("[/list]\n");
            string s = sb.ToString().Trim();
            return s.Length > 7900 ? s.Substring(0, 7900) : s;
        }
    }
}
