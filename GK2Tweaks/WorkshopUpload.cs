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

        // Die kleinen Steamworks-Methoden werden vom Mono-JIT inline eingebaut, Patches darauf greifen nicht.
        // Deshalb im Uploader des Spiels (SubmitContent) die Aufrufe per Transpiler auf eigene Wrapper umleiten.
        internal static void Apply(Harmony h)
        {
            try
            {
                var target = AccessTools.Method(typeof(SteamWorkshopCreatorService), "SubmitContent");
                if (target == null) { Plugin.Log.LogWarning("Workshop upload: SubmitContent not found"); return; }
                h.Patch(target, transpiler: new HarmonyMethod(typeof(WorkshopUpload), nameof(Transpiler)));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Workshop upload patch: " + e.Message); }
        }

        private static System.Collections.Generic.IEnumerable<CodeInstruction> Transpiler(System.Collections.Generic.IEnumerable<CodeInstruction> code)
        {
            int n = 0;
            foreach (CodeInstruction ci in code)
            {
                if ((ci.opcode == System.Reflection.Emit.OpCodes.Call) && ci.operand is System.Reflection.MethodInfo m && m.DeclaringType == typeof(SteamUGC))
                {
                    var w = AccessTools.Method(typeof(WorkshopUpload), "W" + m.Name);
                    if (w != null) { ci.operand = w; n++; }
                }
                yield return ci;
            }
            Plugin.Log.LogInfo("Workshop upload: " + n + " calls redirected");
        }

        private static bool WSetItemTitle(UGCUpdateHandle_t h, string t) { title = t; contentFolder = null; return SteamUGC.SetItemTitle(h, t); }

        private static bool WSetItemDescription(UGCUpdateHandle_t h, string d)
        {
            if (Active)
            {
                try
                {
                    string desc = File.ReadAllText(DescriptionFile, Encoding.UTF8).Replace("\r\n", "\n");
                    Plugin.Log.LogInfo("Workshop upload: description from file (" + desc.Length + " chars)");
                    return SteamUGC.SetItemDescription(h, desc);
                }
                catch (Exception e) { Plugin.Log.LogWarning("Workshop upload: " + e.Message); }
            }
            return SteamUGC.SetItemDescription(h, d);
        }

        private static bool WSetItemVisibility(UGCUpdateHandle_t h, ERemoteStoragePublishedFileVisibility v)
        {
            if (Active) { Plugin.Log.LogInfo("Workshop upload: visibility left unchanged"); return true; }
            return SteamUGC.SetItemVisibility(h, v);
        }

        private static bool WSetItemContent(UGCUpdateHandle_t h, string folder) { contentFolder = folder; return SteamUGC.SetItemContent(h, folder); }

        private static SteamAPICall_t WSubmitItemUpdate(UGCUpdateHandle_t h, string note)
        {
            if (Active)
            {
                try
                {
                    string cl = ChangeNote(contentFolder);
                    if (!string.IsNullOrEmpty(cl)) { note = cl; Plugin.Log.LogInfo("Workshop upload: change note from changelog"); }
                }
                catch (Exception e) { Plugin.Log.LogWarning("Workshop upload: " + e.Message); }
            }
            return SteamUGC.SubmitItemUpdate(h, note);
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
