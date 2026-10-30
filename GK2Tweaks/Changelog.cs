using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GK2Tweaks
{
    // "Was ist neu?": der Changelog steckt als Ressource in der DLL. Nach einem Update (auch per Auto-Update)
    // wird er einmal automatisch im Hauptmenue gezeigt, sonst ueber den Button im Mod-Menue.
    internal static class Changelog
    {
        internal sealed class Entry
        {
            public string Version, Date, En, De;
            public bool Silent; // "<!-- silent -->" in der Ueberschrift: kleiner Hotfix, "Was ist neu?" oeffnet sich dafuer nicht von selbst
            public string Text => Labels.German && !string.IsNullOrEmpty(De) ? De : En;
        }

        private static List<Entry> entries;
        internal static bool FreshInstall;

        internal static List<Entry> Entries
        {
            get
            {
                if (entries == null) entries = Parse(Load());
                return entries;
            }
        }

        private static string Load()
        {
            try
            {
                using (Stream s = typeof(Changelog).Assembly.GetManifestResourceStream("GK2Tweaks.CHANGELOG.md"))
                using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
            }
            catch { return ""; }
        }

        private static readonly Regex Head = new Regex(@"^##\s+(\d+\.\d+\.\d+)\s*(?:–|-)\s*(\S+)");
        private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*");

        // Warnzeichen (mit/ohne Emoji-Variante und folgendem Leerzeichen)
        private static readonly string[] Warn = { ((char)0x26A0).ToString() + (char)0xFE0F + " ", ((char)0x26A0).ToString() + " ", ((char)0x26A0).ToString() + (char)0xFE0F, ((char)0x26A0).ToString() };

        private static List<Entry> Parse(string md)
        {
            var list = new List<Entry>();
            Entry cur = null;
            StringBuilder en = null, de = null, target = null;
            void Flush()
            {
                if (cur == null) return;
                cur.En = en.ToString().TrimEnd();
                cur.De = de.ToString().TrimEnd();
                list.Add(cur);
            }
            foreach (string raw in md.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                Match m = Head.Match(line);
                if (m.Success)
                {
                    Flush();
                    cur = new Entry { Version = m.Groups[1].Value, Date = m.Groups[2].Value, Silent = line.Contains("<!-- silent -->") };
                    en = new StringBuilder(); de = new StringBuilder(); target = en;
                    continue;
                }
                if (cur == null) continue;
                if (line == "**EN**") { target = en; continue; }
                if (line == "**DE**") { target = de; continue; }
                if (line.Length == 0) continue;
#if NEXUS
                // Nexus-Ausgabe hat keinen Updater - Zeilen dazu weglassen
                string low = line.ToLowerInvariant();
                if (low.Contains("github") || low.Contains("update check") || low.Contains("update-prüfung") || low.Contains("online")) continue;
                // ... und keinen Bewertungshinweis fuer den Steam Workshop
                if (low.Contains("rating on the steam workshop") || low.Contains("bewertung im steam workshop")) continue;
#endif
                // Symbole wie ⚠ kommen im Spiel aus einer Ersatzschrift und werden beim Scrollen nicht abgeschnitten (wandern aus dem Fenster) -> weglassen, die Farbe hebt hervor
                foreach (string w in Warn) line = line.Replace(w, "");
                if (line.StartsWith("- ")) line = "•  " + line.Substring(2);
                else if (line.StartsWith("> ")) line = "<color=#f0b060>" + line.Substring(2) + "</color>";   // hervorgehobener Hinweis
                target.AppendLine(Bold.Replace(line, "<b>$1</b>"));
            }
            Flush();
            return list;
        }

        // einmal nach einem Update zeigen
        internal static bool ShouldAutoShow()
        {
            string seen = Plugin.LastSeenVersion.Value;
            if (seen == Plugin.PluginVersion) return false;
            if (FreshInstall) { Plugin.LastSeenVersion.Value = Plugin.PluginVersion; return false; }
            // nur stille Hotfixes seit der letzten gesehenen Version -> nicht aufpoppen, trotzdem als gesehen merken
            bool anyLoud = false;
            foreach (Entry e in Since(seen)) if (!e.Silent) { anyLoud = true; break; }
            if (!anyLoud) { MarkSeen(); return false; }
            return true;
        }

        internal static void MarkSeen() => Plugin.LastSeenVersion.Value = Plugin.PluginVersion;

        // Nur Versionen, die neuer sind als die zuletzt gesehene (mindestens die aktuelle)
        internal static List<Entry> Since(string seen)
        {
            var result = new List<Entry>();
            Version.TryParse(seen ?? "", out Version from);
            foreach (Entry e in Entries)
            {
                if (!Version.TryParse(e.Version, out Version v)) continue;
                if (from == null || v > from || result.Count == 0) result.Add(e);
            }
            return result;
        }
    }
}
