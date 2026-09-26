using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2Tweaks
{
    // Anzeigenamen fuer das Mod-Menue (Deutsch/Englisch). Die Config-Datei nutzt englische Schluessel und Beschreibungen.
    internal static class Labels
    {
        // Sprachcode der Mod-Texte: de, en oder eine Sprache aus einer Sprachdatei (fr, es, ru, zh ...)
        internal static string Lang
        {
            get
            {
                string l = Plugin.Language?.Value ?? "Auto";
                switch (l)
                {
                    case "Deutsch": return "de";
                    case "English": return "en";
                    case "Français": return "fr";
                    case "Español": return "es";
                    case "Русский": return "ru";
                    case "中文": return "zh";
                }
                string g = null;
                try { g = GameSettings.Instance?.language; } catch { }
                if (string.IsNullOrEmpty(g))
                {
                    switch (Application.systemLanguage)
                    {
                        case SystemLanguage.German: return "de";
                        case SystemLanguage.French: return "fr";
                        case SystemLanguage.Spanish: return "es";
                        case SystemLanguage.Russian: return "ru";
                        case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: case SystemLanguage.ChineseTraditional: return "zh";
                        default: return "en";
                    }
                }
                g = g.ToLowerInvariant();
                if (g.StartsWith("de")) return "de";
                if (g.StartsWith("zh")) return Translations.Has("zh") ? "zh" : "en";
                string code = g.Length >= 2 ? g.Substring(0, 2) : g;
                return Translations.Has(code) ? code : "en";
            }
        }

        internal static bool German => Lang == "de";

        // Deutsch steht im Code, Englisch ebenfalls; alle anderen Sprachen kommen aus Sprachdateien (Schluessel = englischer Text)
        internal static string T(string de, string en)
        {
            string l = Lang;
            if (l == "de") return de;
            if (l == "en") return en;
            return Translations.Get(l, en);
        }

        // key -> (Deutsch, English)
        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { "Mode", new[] { "Bildraten-Modus", "Frame rate mode" } },
            { "TargetFps", new[] { "Ziel-FPS", "Target FPS" } },
            { "RenderMode", new[] { "Render-Modus", "Render mode" } },
            { "Shadows", new[] { "Schatten", "Shadows" } },
            { "AmbientOcclusion", new[] { "Umgebungsverdeckung (HBAO)", "Ambient occlusion (HBAO)" } },
            { "PointLights", new[] { "Punktlichter", "Point lights" } },
            { "BackLight", new[] { "Gegenlicht", "Back light" } },
            { "Water", new[] { "Wasser", "Water" } },
            { "Clouds", new[] { "Wolken", "Clouds" } },
            { "PhysicsHz", new[] { "Physik-Takt", "Physics rate" } },
            { "Zoom", new[] { "Kamera-Zoom", "Camera zoom" } },
            { "InteriorZoom", new[] { "Zoom in Innenräumen", "Zoom indoors" } },
            { "ZoomPresets", new[] { "Zoom-Stufen", "Zoom presets" } },
            { "ZoomPresetKey", new[] { "Taste Zoom-Stufe", "Zoom preset key" } },
            { "MouseWheelZoom", new[] { "Zoom mit Mausrad", "Mouse wheel zoom" } },
            { "SmoothZoom", new[] { "Weicher Zoom", "Smooth zoom" } },
            { "MenuScale", new[] { "Größe der Mod-Anzeigen", "Size of mod displays" } },
            { "HighContrast", new[] { "Hoher Kontrast", "High contrast" } },
            { "Key", new[] { "Taste Screenshot", "Screenshot key" } },
            { "Scale", new[] { "Auflösung", "Resolution" } },
            { "HideHud", new[] { "Ohne HUD", "Without HUD" } },
            { "PauseInBackground", new[] { "Pause im Hintergrund", "Pause in background" } },
            { "AutoSaveMinutes", new[] { "Autosave", "Autosave" } },
            { "MainMenuExtend", new[] { "Hauptmenü verbreitern", "Widen main menu" } },
            { "SkipIntro", new[] { "Logos überspringen", "Skip logos" } },
            { "MainMenuModdedLabel", new[] { "Hinweis \"modded\"", "\"modded\" note" } },
            { "GameLog", new[] { "Spiel-Log", "Game log" } },
            { "GameMenuButton", new[] { "Button \"Mods\" im Menü", "\"Mods\" button in menus" } },
            { "ShowOverlay", new[] { "FPS-Anzeige zeigen", "Show FPS display" } },
            { "StatsLogSeconds", new[] { "Statistik ins Log", "Stats to log" } },
            { "MenuKey", new[] { "Taste Mod-Menü", "Mod menu key" } },
            { "OverlayKey", new[] { "Taste FPS-Anzeige", "FPS display key" } },
            { "SaveKey", new[] { "Taste Speichern", "Save key" } },
            { "WeekPlanKey", new[] { "Taste Wochenplan", "Week plan key" } },
            { "HideHudKey", new[] { "Taste HUD ausblenden", "Hide HUD key" } },
            { "DailyReminder", new[] { "Tagesübersicht am Morgen", "Daily reminder" } },
            { "KeepBackups", new[] { "Backups pro Spielstand", "Backups per save" } },
            { "MinMinutesBetween", new[] { "Mindestabstand", "Minimum interval" } },
            { "CheckForUpdates", new[] { "Nach Updates suchen", "Check for updates" } },
            { "Language", new[] { "Sprache", "Language" } },
            { "Corner", new[] { "Position", "Position" } },
            { "Enabled", new[] { "Anpinnen", "Pinning" } },
            { "Size", new[] { "Größe", "Size" } },
            { "Layout", new[] { "Anordnung", "Layout" } },
            { "Fps", new[] { "FPS", "FPS" } },
            { "Lows", new[] { "1%-Low-FPS", "1% low FPS" } },
            { "FrameTime", new[] { "Frametime (ms)", "Frame time (ms)" } },
            { "Cpu", new[] { "CPU-Last", "CPU load" } },
            { "Gpu", new[] { "GPU-Last", "GPU load" } },
            { "Ram", new[] { "Arbeitsspeicher (RAM)", "Memory (RAM)" } },
            { "Vram", new[] { "Grafikspeicher (VRAM)", "Video memory (VRAM)" } },
            { "Resolution", new[] { "Auflösung", "Resolution" } },
            { "Clock", new[] { "Uhrzeit", "Clock" } },
            { "Weekday", new[] { "Wochentag (im Spiel)", "Weekday (in game)" } },
            { "GameTime", new[] { "Uhrzeit (im Spiel)", "Time of day (in game)" } },
            { "CustomResolution", new[] { "Zusätzliche Auflösungen", "Extra resolutions" } },
            { "MainMenuScaleX2", new[] { "Hauptmenü-Skalierung", "Main menu scaling" } },
        };

        // Deutsche Tooltips (Englisch = Beschreibung aus der Config)
        private static readonly Dictionary<string, string> TipsDe = new Dictionary<string, string>
        {
            { "Mode", "Spiel = Einstellung aus dem Spielmenü. Software-Limit = Begrenzung auf Ziel-FPS. VSync = an die Bildwiederholrate gekoppelt (60 FPS bei 120 Hz = jedes 2. Bild)." },
            { "TargetFps", "Ziel-FPS für Software-Limit und VSync. Unbegrenzt = volle Bildwiederholrate." },
            { "RenderMode", "Voll = Welt in voller Auflösung. Pixel = Welt in halber Auflösung, deutlich weniger GPU-Last." },
            { "Shadows", "PC weich = aufwendige PC-Schatten. Einfach/Konsole = einfachere Schatten wie auf PS5/Xbox, spart viel Leistung." },
            { "AmbientOcclusion", "Umgebungsverdeckung (HBAO). \"Aus\" gibt es im Spiel sonst nur auf der Switch." },
            { "PointLights", "Punktlichter echt oder simuliert (günstiger)." },
            { "BackLight", "Gegenlicht-Effekt." },
            { "Water", "Wasserqualität." },
            { "Clouds", "Wolken normal oder vereinfacht. Wirkt teils erst nach Gebietswechsel." },
            { "PhysicsHz", "Physik-Takt. 30 Hz wie die Switch-Version, spart CPU." },
            { "GameLog", "Was das Spiel ins Player.log schreibt. Weniger Log = weniger Ruckler durch Log-Spam." },
            { "Zoom", "Unter 100 % = mehr Umgebung sichtbar, über 100 % = näher dran." },
            { "InteriorZoom", "Eigener Zoom in Gebäuden (Kirche, Leichenhalle, Häuser …). \"wie draußen\" = kein Unterschied." },
            { "ZoomPresets", "Zoom-Stufen in Prozent, durch die die Taste schaltet, mit Komma getrennt (z. B. 80,100,125)." },
            { "ZoomPresetKey", "Schaltet durch die Zoom-Stufen." },
            { "MouseWheelZoom", "Stufenlos zoomen mit dem Mausrad – nur beim Herumlaufen, nicht über Menüs. Beim nächsten Start gilt wieder der Kamera-Zoom." },
            { "SmoothZoom", "Weicher Übergang, wenn sich der Zoom ändert." },
            { "MenuScale", "Größe von Mod-Menü, FPS-Anzeige und Pin-Liste. Automatisch = passend zur Bildschirmhöhe." },
            { "HighContrast", "Stärkerer Kontrast: dunkler Hintergrund für die Pin-Liste, fette und hellere Haben/Brauchen-Zahlen, größere Tooltips." },
            { "Key", "Taste für einen Screenshot (gespeichert in BepInEx/GK2VanillaPlus/Screenshots)." },
            { "Scale", "Auflösungs-Faktor: 2× = doppelte Bildschirmauflösung in jede Richtung (z. B. 3840×2160 aus 1920×1080)." },
            { "HideHud", "Blendet für den Screenshot die Spiel-Oberfläche und die Mod-Anzeigen aus." },
            { "PauseInBackground", "Spiel pausiert, wenn das Fenster nicht im Vordergrund ist (spart Akku und Hitze)." },
            { "AutoSaveMinutes", "Zusätzlicher Autosave. Speichert nur, wenn du frei steuerbar bist." },
            { "MainMenuExtend", "Füllt auf Ultrawide-Bildschirmen die Seiten des Hauptmenüs mit einer unscharfen Kopie des Menübilds." },
            { "SkipIntro", "Logos und Intro-Videos beim Spielstart überspringen." },
            { "GameMenuButton", "Zeigt im Hauptmenü und im Pausenmenü (Esc) einen Button \"Mods\", der dieses Menü öffnet." },
            { "MainMenuModdedLabel", "Hinweis \"modded\" neben der Versionsnummer im Hauptmenü." },
#if !NEXUS
            { "CheckForUpdates", "Prüft bei jedem Spielstart einmal auf GitHub, ob es eine neue Version gibt. Es wird nur die Versionsnummer gelesen, nichts gesendet." },
#endif
            { "WeekPlanKey", "Öffnet den Wochenplan: was an welchem Wochentag möglich ist (nur bereits Freigeschaltetes)." },
            { "HideHudKey", "Blendet die komplette Spiel-Oberfläche aus, z. B. für Screenshots. Esc blendet sie wieder ein." },
            { "DailyReminder", "Jeden Morgen eine Benachrichtigung, was heute möglich ist – nur bereits freigeschaltete Dinge, keine Spoiler." },
            { "KeepBackups", "Bevor das Spiel einen Spielstand überschreibt, wird der alte gesichert (BepInEx/GK2VanillaPlus/Backups). 0 = aus." },
            { "MinMinutesBetween", "Mindestabstand zwischen zwei Backups desselben Spielstands, damit nicht jeder Autosave eins erzeugt." },
            { "SaveKey", "Taste zum manuellen Speichern (Esc beim Zuweisen = keine Taste). Ohne Taste nur über den Button im Mod-Menü." },
            { "ShowOverlay", "FPS-Anzeige ein- oder ausblenden (auch mit F10)." },
            { "StatsLogSeconds", "Frame-Statistik regelmäßig ins BepInEx-Log schreiben." },
            { "Language", "Sprache des Mod-Menüs. Automatisch = Sprache des Spiels." },
            { "Corner", "In welcher Bildschirmecke die Anzeige steht." },
            { "Enabled", "Rezepte, Baupläne und Stadtgebäude über die Pinnadel oben rechts anpinnen. Die Liste zeigt pro Zutat Haben/Brauchen aus deinem Inventar (ohne Truhen)." },
            { "Size", "Text- und Symbolgröße der Pin-Liste." },
            { "Layout", "Nebeneinander = alles in einer Zeile. Untereinander = ein Wert pro Zeile." },
            { "Fps", "Bilder pro Sekunde (Durchschnitt über 0,5 s)." },
            { "Lows", "1%-Low: wie flüssig es sich anfühlt. Nah am FPS-Wert = keine Ruckler." },
            { "FrameTime", "Zeit pro Bild in ms (Durchschnitt und langsamstes Bild)." },
            { "Cpu", "CPU-Last des Spiels (100 % = alle Kerne voll ausgelastet)." },
            { "Gpu", "GPU-Last (3D, wie im Task-Manager). Nur unter Windows." },
            { "Ram", "Arbeitsspeicher, den das Spiel belegt / eingebauter RAM." },
            { "Vram", "Grafikspeicher für Texturen und Puffer des Spiels / Speicher der Grafikkarte." },
            { "Resolution", "Aktuelle Auflösung." },
            { "Clock", "Aktuelle Uhrzeit (echte Uhr)." },
            { "Weekday", "Aktueller Wochentag im Spiel (Hochmut, Trägheit, …)." },
            { "GameTime", "Tageszeit im Spiel, in 10-Minuten-Schritten." },
            { "CustomResolution", "Zusätzliche Auflösung(en), komma-getrennt, z. B. 3840x1080. Wirkt nach Neustart des Spiels." },
            { "MainMenuScaleX2", "Skalierung der Hauptmenü-Szene bei freigeschalteten Auflösungen." },
        };

        private static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>
        {
            { "Default", new[] { "wie Grafikstufe", "as graphics tier" } },
            { "Game", new[] { "wie im Spielmenü", "game setting" } },
            { "Limit", new[] { "Software-Limit", "Software limit" } },
            { "VSync", new[] { "VSync", "VSync" } },
            { "Off", new[] { "Aus", "Off" } },
            { "On", new[] { "An", "On" } },
            { "Auto", new[] { "Automatisch", "Automatic" } },
            { "NGSS_High", new[] { "PC weich (hoch)", "PC soft (high)" } },
            { "NGSS_Medium", new[] { "PC weich (mittel)", "PC soft (medium)" } },
            { "NGSS_Low", new[] { "PC weich (niedrig)", "PC soft (low)" } },
            { "Unity_Desktop", new[] { "Einfach (Desktop)", "Simple (desktop)" } },
            { "Unity_Balanced", new[] { "Einfach (ausgewogen)", "Simple (balanced)" } },
            { "Unity_Performance", new[] { "Einfach (Leistung)", "Simple (performance)" } },
            { "Unity_Low", new[] { "Einfach (niedrig)", "Simple (low)" } },
            { "Unity_Minimal", new[] { "Einfach (minimal)", "Simple (minimal)" } },
            { "Unity_Console", new[] { "Konsole (PS5/Xbox)", "Console (PS5/Xbox)" } },
            { "Lowest", new[] { "Niedrigste", "Lowest" } },
            { "Low", new[] { "Niedrig", "Low" } },
            { "Medium", new[] { "Mittel", "Medium" } },
            { "High", new[] { "Hoch", "High" } },
            { "Highest", new[] { "Höchste", "Highest" } },
            { "Realtime", new[] { "Echt", "Real" } },
            { "Faked", new[] { "Simuliert", "Simulated" } },
            { "Light", new[] { "Einfach", "Simple" } },
            { "Normal", new[] { "Normal", "Normal" } },
            { "Replaced", new[] { "Vereinfacht", "Simplified" } },
            { "Native", new[] { "Voll (nativ)", "Full (native)" } },
            { "Lightweight", new[] { "Pixel (halbe Aufl.)", "Pixel (half res)" } },
            { "All", new[] { "Alles", "Everything" } },
            { "Warning", new[] { "Warnungen + Fehler", "Warnings + errors" } },
            { "Error", new[] { "Nur Fehler", "Errors only" } },
            { "Deutsch", new[] { "Deutsch", "Deutsch" } },
            { "Français", new[] { "Français", "Français" } },
            { "Español", new[] { "Español", "Español" } },
            { "Русский", new[] { "Русский", "Русский" } },
            { "中文", new[] { "中文", "中文" } },
            { "English", new[] { "English", "English" } },
            { "TopLeft", new[] { "Oben links", "Top left" } },
            { "TopRight", new[] { "Oben rechts", "Top right" } },
            { "BottomLeft", new[] { "Unten links", "Bottom left" } },
            { "BottomRight", new[] { "Unten rechts", "Bottom right" } },
            { "Row", new[] { "Nebeneinander", "In a row" } },
            { "Small", new[] { "Klein", "Small" } },
            { "Large", new[] { "Groß", "Large" } },
            { "ExtraLarge", new[] { "Sehr groß", "Extra large" } },
            { "Column", new[] { "Untereinander", "In a column" } },
        };

        internal static string Name(ConfigEntryBase e)
        {
            string key = e.Definition.Key;
            return Names.TryGetValue(key, out string[] n) ? T(n[0], n[1]) : key;
        }

        internal static string Tip(ConfigEntryBase e)
        {
            if (German && TipsDe.TryGetValue(e.Definition.Key, out string t)) return t;
            string d = e.Description?.Description ?? "";
            int slash = d.IndexOf(" / ");
            if (slash > 0) d = d.Substring(0, slash);
            string l = Lang;
            return l == "de" || l == "en" ? d : Translations.Get(l, d);
        }

        internal static string Value(ConfigEntryBase e, object value)
        {
            string key = e.Definition.Key;
            if (value is int i)
            {
                if (key == "TargetFps") return i == 0 ? T("Unbegrenzt", "Unlimited") : i + " FPS";
                if (key == "PhysicsHz") return i == 0 ? T("Standard (50 Hz)", "Default (50 Hz)") : i + " Hz";
                if (key == "Zoom") return i + " %";
                if (key == "InteriorZoom") return i == 0 ? T("wie draußen", "same as outside") : i + " %";
                if (key == "MenuScale") return i == 0 ? T("Automatisch", "Automatic") : i + " %";
                if (key == "Scale") return i + "×";
                if (key == "AutoSaveMinutes") return i == 0 ? T("Aus", "Off") : T("alle ", "every ") + i + " min";
                if (key == "KeepBackups") return i == 0 ? T("Aus", "Off") : i.ToString();
                if (key == "MinMinutesBetween") return i == 0 ? T("jedes Speichern", "every save") : i + " min";
                if (key == "StatsLogSeconds") return i == 0 ? T("Aus", "Off") : T("alle ", "every ") + i + " s";
                return i.ToString();
            }
            string s = value?.ToString() ?? "";
            return Values.TryGetValue(s, out string[] v) ? T(v[0], v[1]) : s;
        }

        internal static string Tier(GraphicsTier t)
        {
            switch (t)
            {
                case GraphicsTier.High: return T("Hoch", "High");
                case GraphicsTier.Medium: return T("Mittel", "Medium");
                case GraphicsTier.Low: return T("Niedrig", "Low");
                default: return T("Niedrigste", "Lowest");
            }
        }
    }

    // Sprachdateien: eingebaut (lang/xx.txt in der DLL) und ueberschreibbar durch BepInEx/GK2VanillaPlus/lang/xx.txt.
    // Format pro Zeile:  English text => Uebersetzung      (# = Kommentar, \n = Zeilenumbruch)
    internal static class Translations
    {
        private static readonly Dictionary<string, Dictionary<string, string>> cache = new Dictionary<string, Dictionary<string, string>>();
        internal static readonly string[] Shipped = { "fr", "es", "ru", "zh" };
        internal static string Folder => System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus", "lang");

        internal static bool Has(string code) => Load(code).Count > 0;

        internal static void Reload() => cache.Clear();

        internal static string Get(string code, string en)
        {
            if (string.IsNullOrEmpty(en)) return en;
            var d = Load(code);
            if (d.Count == 0) return en;
            string core = en.Trim();
            if (!d.TryGetValue(core, out string tr) || string.IsNullOrEmpty(tr)) return en;
            if (core.Length == en.Length) return tr;
            int lead = en.Length - en.TrimStart().Length, trail = en.Length - en.TrimEnd().Length;
            return en.Substring(0, lead) + tr + en.Substring(en.Length - trail);
        }

        private static Dictionary<string, string> Load(string code)
        {
            if (cache.TryGetValue(code, out var d)) return d;
            d = new Dictionary<string, string>();
            try
            {
                using (var s = typeof(Translations).Assembly.GetManifestResourceStream("GK2Tweaks.lang." + code + ".txt"))
                    if (s != null) using (var r = new System.IO.StreamReader(s, System.Text.Encoding.UTF8)) Parse(r.ReadToEnd(), d);
            }
            catch { }
            try
            {
                string f = System.IO.Path.Combine(Folder, code + ".txt");
                if (System.IO.File.Exists(f)) Parse(System.IO.File.ReadAllText(f, System.Text.Encoding.UTF8), d);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Language file " + code + ": " + e.Message); }
            cache[code] = d;
            return d;
        }

        private static void Parse(string text, Dictionary<string, string> d)
        {
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0 || raw.StartsWith("#")) continue;
                int i = raw.IndexOf(" => ", System.StringComparison.Ordinal);
                if (i <= 0) continue;
                string k = raw.Substring(0, i).Replace("\\n", "\n").Trim();
                string v = raw.Substring(i + 4).Replace("\\n", "\n").Trim();
                if (k.Length > 0 && v.Length > 0) d[k] = v;
            }
        }
    }
}
