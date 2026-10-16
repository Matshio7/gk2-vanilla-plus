using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2Tweaks
{
    // Tag & Uhrzeit am HUD: als zweite Zeile in der Gebietsanzeige des Spiels oben rechts ("Herms Laden" /
    // "Tag 158   14:10") - Wo und Wann an einer Stelle, im Rahmen und in der Schrift des Spiels.
    // Wir haengen nur Text an das vorhandene Label an; das Spiel schreibt den Gebietsnamen bei jedem Gebietswechsel
    // neu (WorldZoneWidget.Redraw), danach setzen wir die Zeile wieder dazu. Nichts wird kopiert oder entfernt.
    // Idee aus dem Nexus-Mod "What time is it" - eigene Umsetzung.
    internal static class HudClock
    {
        internal const string Mark = "<size=82%><color=#d8ccb0>";
        private static float next;
        private static string lastLine;
        private static bool otherChecked;
        // Kann die Schrift des Spiels unsere Zeile nicht zeichnen (TMP wirft in SetArraySizes, z. B. Ersatzschrift
        // ohne Material - SUNA), stufen wir ab: 0 = volle Zeile, 1 = nur Uhrzeit aus Ziffern, 2 = keine Zeile.
        // Gilt bis die Schrift wechselt (Sprache umgestellt).
        private static int level;
        private static TMP_FontAsset failFont;
        internal static int Level => level;
#if DEV
        internal static int TestThrow;   // Dev-Test: die naechsten n Zeichenversuche wie bei SUNA scheitern lassen
        internal static void TestReset() { level = 0; failFont = null; next = 0f; }
#endif

        // "What time is it" ist zusaetzlich installiert -> keine doppelte Anzeige
        internal static bool OtherMod { get; private set; }

        private static TMP_Text Label(out WorldZoneWidget w)
        {
            w = GUIElements.Instance != null ? GUIElements.Instance.WorldZoneWidget : null;
            return w != null ? Traverse.Create(w).Field("worldZoneLabel").GetValue<TMP_Text>() : null;
        }

        // Text des Spiels ohne unsere Zeile
        internal static string Strip(string t)
        {
            if (t == null) return null;
            int i = t.IndexOf("\n" + Mark, StringComparison.Ordinal);
            return i >= 0 ? t.Substring(0, i) : t;
        }

        internal static bool Want => Plugin.HudClock.Value && WeekPlan.InGame && !OtherMod;

        internal static void Tick()
        {
            if (!otherChecked && WeekPlan.InGame)
            {
                otherChecked = true;
                OtherMod = ModCompat.Has("what", "time");
                if (OtherMod) Plugin.Log.LogInfo("HUD clock: 'What time is it' is installed, own HUD line stays off");
            }
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            TMP_Text t = Label(out WorldZoneWidget w);
            if (t == null) return;
            if (level > 0 && t.font != failFont) level = 0;   // andere Schrift (Sprache gewechselt): neu versuchen
            string cur = t.text ?? "";
            string bas = Strip(cur);
            string line = Want && level < 2 ? Compose(level == 1) : null;
            string target = string.IsNullOrEmpty(line) || string.IsNullOrEmpty(bas) ? bas : bas + "\n" + Mark + line + "</color></size>";
            if (target == cur) return;
            try
            {
                t.text = target;
#if DEV
                if (TestThrow > 0 && line != null) { TestThrow--; throw new NullReferenceException("dev test (TMP SetArraySizes)"); }
#endif
                t.ForceMeshUpdate(true, true);
            }
            catch (Exception e)
            {
                // Text des Spiels wiederherstellen, sonst wirft TMP bei jedem Neuzeichnen erneut (Dauer-Warnungen im Log)
                try { t.text = bas; t.ForceMeshUpdate(true, true); } catch { }
                if (line == null) return;
                level++;
                failFont = t.font;
                next = 0f;
                Plugin.Log.LogWarning("HUD clock: the game font " + (t.font != null ? "'" + t.font.name + "' " : "") + "could not draw '" + line + "'"
                    + Missing(t, line) + " (" + e.GetType().Name + ": " + e.Message + ") - "
                    + (level >= 2 ? "HUD line stays off until the language changes" : "showing the time in digits only"));
                return;
            }
            try { UIExtensions.RefreshContentFitter((RectTransform)w.transform); } catch { LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)w.transform); }
            if (lastLine == null && line != null) Plugin.Log.LogInfo("HUD clock: shown in the area name box" + (level == 1 ? " (digits only)" : ""));
            lastLine = line;
        }

        // fuer das Log: Zeichen, die in der Schrift selbst fehlen (TMP sucht sie dann in Ersatzschriften)
        private static string Missing(TMP_Text t, string line)
        {
            try
            {
                if (t.font == null || t.font.HasCharacters(line, out System.Collections.Generic.List<char> miss) || miss == null || miss.Count == 0) return "";
                return ", missing in the font: '" + new string(miss.ToArray()) + "'";
            }
            catch { return ""; }
        }

        // nach einem Redraw des Spiels sofort wieder anhaengen (kein Flackern)
        internal static void AfterRedraw() { next = 0f; try { Tick(); } catch { } }

        // digitsOnly: nur "14:10" - fuer Schriften, die unsere Buchstaben nicht zeichnen koennen
        private static string Compose(bool digitsOnly = false)
        {
            try
            {
                EnvironmentData env = MainGame.Instance.GameSave.environmentData;
                int min = Mathf.FloorToInt(Mathf.Repeat(env.TimeOfDay, 1f) * 1440f) / 10 * 10;
                int h = min / 60, m = min % 60;
                if (digitsOnly) return h.ToString("00") + ":" + m.ToString("00");
                string time = Plugin.HudClock12h.Value
                    ? ((h + 11) % 12 + 1) + ":" + m.ToString("00") + (h < 12 ? " AM" : " PM")
                    : h.ToString("00") + ":" + m.ToString("00");
                switch (Plugin.HudClockMode.Value)
                {
                    case "TimeOnly": return time;
                    case "WeekdayAndTime":
                        string id = WeekPlan.IdForNumber(env.CurrentDayNumber);
                        return id != null ? WeekPlan.DayName(id) + "   " + time : time;
                    default:
                        return string.Format(Labels.T("Tag {0}", "Day {0}"), env.Day) + "   " + time;
                }
            }
            catch { return null; }
        }
    }

    [HarmonyPatch(typeof(WorldZoneWidget), nameof(WorldZoneWidget.Redraw))]
    internal static class ZoneRedrawPatch
    {
        private static void Postfix() { HudClock.AfterRedraw(); }
    }

    // Erkennt andere BepInEx-Mods am Namen (fuer Mods mit gleicher Funktion: dann keine doppelte Wirkung)
    internal static class ModCompat
    {
        internal static bool Has(params string[] words)
        {
            try
            {
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    var md = kv.Value?.Metadata;
                    if (md == null || md.GUID == Plugin.Guid) continue;
                    string n = (md.Name + " " + md.GUID).ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "");
                    bool all = true;
                    foreach (string w in words) if (n.IndexOf(w, StringComparison.Ordinal) < 0) { all = false; break; }
                    if (all) return true;
                }
            }
            catch { }
            return false;
        }
    }
}
