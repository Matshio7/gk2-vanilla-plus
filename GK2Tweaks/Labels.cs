using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2Tweaks
{
    // Anzeigenamen fuer das Mod-Menue (Deutsch/Englisch). Die Config-Datei nutzt englische Schluessel und Beschreibungen.
    internal static class Labels
    {
        internal static bool German
        {
            get
            {
                string l = Plugin.Language?.Value ?? "Auto";
                if (l == "Deutsch") return true;
                if (l == "English") return false;
                try
                {
                    string g = GameSettings.Instance?.language;
                    if (!string.IsNullOrEmpty(g)) return g.StartsWith("de", System.StringComparison.OrdinalIgnoreCase);
                }
                catch { }
                return Application.systemLanguage == SystemLanguage.German;
            }
        }

        internal static string T(string de, string en) => German ? de : en;

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
            { "PauseInBackground", new[] { "Pause im Hintergrund", "Pause in background" } },
            { "AutoSaveMinutes", new[] { "Autosave", "Autosave" } },
            { "MainMenuExtend", new[] { "Hauptmenü verbreitern", "Widen main menu" } },
            { "SkipIntro", new[] { "Logos überspringen", "Skip logos" } },
            { "MainMenuModdedLabel", new[] { "Hinweis \"modded\"", "\"modded\" note" } },
            { "GameLog", new[] { "Spiel-Log", "Game log" } },
            { "ShowOverlay", new[] { "FPS-Anzeige zeigen", "Show FPS display" } },
            { "StatsLogSeconds", new[] { "Statistik ins Log", "Stats to log" } },
            { "MenuKey", new[] { "Taste Mod-Menü", "Mod menu key" } },
            { "OverlayKey", new[] { "Taste FPS-Anzeige", "FPS display key" } },
            { "SaveKey", new[] { "Taste Speichern", "Save key" } },
            { "CheckForUpdates", new[] { "Nach Updates suchen", "Check for updates" } },
            { "Language", new[] { "Sprache", "Language" } },
            { "Corner", new[] { "Position", "Position" } },
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
            { "PauseInBackground", "Spiel pausiert, wenn das Fenster nicht im Vordergrund ist (spart Akku und Hitze)." },
            { "AutoSaveMinutes", "Zusätzlicher Autosave. Speichert nur, wenn du frei steuerbar bist." },
            { "MainMenuExtend", "Füllt auf Ultrawide-Bildschirmen die Seiten des Hauptmenüs mit einer unscharfen Kopie des Menübilds." },
            { "SkipIntro", "Logos und Intro-Videos beim Spielstart überspringen." },
            { "MainMenuModdedLabel", "Hinweis \"modded\" neben der Versionsnummer im Hauptmenü." },
            { "CheckForUpdates", "Prüft bei jedem Spielstart einmal auf GitHub, ob es eine neue Version gibt. Es wird nur die Versionsnummer gelesen, nichts gesendet." },
            { "SaveKey", "Taste zum manuellen Speichern (Esc beim Zuweisen = keine Taste). Ohne Taste nur über den Button im Mod-Menü." },
            { "ShowOverlay", "FPS-Anzeige ein- oder ausblenden (auch mit F10)." },
            { "StatsLogSeconds", "Frame-Statistik regelmäßig ins BepInEx-Log schreiben." },
            { "Language", "Sprache des Mod-Menüs. Automatisch = Sprache des Spiels." },
            { "Corner", "In welcher Bildschirmecke die FPS-Anzeige steht." },
            { "Layout", "Nebeneinander = alles in einer Zeile. Untereinander = ein Wert pro Zeile." },
            { "Fps", "Bilder pro Sekunde (Durchschnitt über 0,5 s)." },
            { "Lows", "1%-Low: wie flüssig es sich anfühlt. Nah am FPS-Wert = keine Ruckler." },
            { "FrameTime", "Zeit pro Bild in ms (Durchschnitt und langsamstes Bild)." },
            { "Cpu", "CPU-Last des Spiels (100 % = alle Kerne voll ausgelastet)." },
            { "Gpu", "GPU-Last (3D, wie im Task-Manager). Nur unter Windows." },
            { "Ram", "Arbeitsspeicher, den das Spiel belegt / eingebauter RAM." },
            { "Vram", "Grafikspeicher für Texturen und Puffer des Spiels / Speicher der Grafikkarte." },
            { "Resolution", "Aktuelle Auflösung." },
            { "Clock", "Aktuelle Uhrzeit." },
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
            { "English", new[] { "English", "English" } },
            { "TopLeft", new[] { "Oben links", "Top left" } },
            { "TopRight", new[] { "Oben rechts", "Top right" } },
            { "BottomLeft", new[] { "Unten links", "Bottom left" } },
            { "BottomRight", new[] { "Unten rechts", "Bottom right" } },
            { "Row", new[] { "Nebeneinander", "In a row" } },
            { "Column", new[] { "Untereinander", "In a column" } },
        };

        internal static string Name(ConfigEntryBase e)
        {
            string key = e.Definition.Key;
            return Names.TryGetValue(key, out string[] n) ? n[German ? 0 : 1] : key;
        }

        internal static string Tip(ConfigEntryBase e)
        {
            if (German && TipsDe.TryGetValue(e.Definition.Key, out string t)) return t;
            string d = e.Description?.Description ?? "";
            int slash = d.IndexOf(" / ");
            return slash > 0 ? d.Substring(0, slash) : d;
        }

        internal static string Value(ConfigEntryBase e, object value)
        {
            string key = e.Definition.Key;
            if (value is int i)
            {
                if (key == "TargetFps") return i == 0 ? T("Unbegrenzt", "Unlimited") : i + " FPS";
                if (key == "PhysicsHz") return i == 0 ? T("Standard (50 Hz)", "Default (50 Hz)") : i + " Hz";
                if (key == "Zoom") return i + " %";
                if (key == "AutoSaveMinutes") return i == 0 ? T("Aus", "Off") : T("alle ", "every ") + i + " min";
                if (key == "StatsLogSeconds") return i == 0 ? T("Aus", "Off") : T("alle ", "every ") + i + " s";
                return i.ToString();
            }
            string s = value?.ToString() ?? "";
            return Values.TryGetValue(s, out string[] v) ? v[German ? 0 : 1] : s;
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
}
