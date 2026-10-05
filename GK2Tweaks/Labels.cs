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
                    case "Português": return "pt";
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
                        case SystemLanguage.Portuguese: return "pt";
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
            { "OledBlack", new[] { "OLED-Schwarz", "OLED black" } },
            { "WideRain", new[] { "Regen über ganze Breite", "Full-width rain" } },
            { "MenuBackground", new[] { "Bild", "Picture" } },
            { "MenuBackgroundStyle", new[] { "Stil", "Style" } },
            { "MenuBackgroundBlur", new[] { "Weichzeichnen", "Blur" } },
            { "MenuBackgroundFog", new[] { "Nebel", "Fog" } },
            { "MenuBackgroundFogTone", new[] { "Nebel-Farbe", "Fog color" } },
            { "MenuBackgroundDim", new[] { "Abdunkeln", "Darken" } },
            { "RainAmount", new[] { "Regen: Menge", "Rain: amount" } },
            { "Key", new[] { "Taste Screenshot", "Screenshot key" } },
            { "Scale", new[] { "Auflösung", "Resolution" } },
            { "HideHud", new[] { "Ohne HUD", "Without HUD" } },
            { "PauseInBackground", new[] { "Pause im Hintergrund", "Pause in background" } },
            { "AutoSaveMinutes", new[] { "Autosave", "Autosave" } },
            { "MainMenuExtend", new[] { "Hauptmenü verbreitern", "Widen main menu" } },
            { "SkipIntro", new[] { "Logos überspringen", "Skip logos" } },
            { "MainMenuModdedLabel", new[] { "Hinweis \"modded\"", "\"modded\" note" } },
            { "GameLog", new[] { "Spiel-Log", "Game log" } },
            { "GameMenuButton", new[] { "Button \"Vanilla+\" im Menü", "\"Vanilla+\" button in menus" } },
            { "ShowOverlay", new[] { "FPS-Anzeige zeigen", "Show FPS display" } },
            { "StatsLogSeconds", new[] { "Statistik ins Log", "Stats to log" } },
            { "MenuKey", new[] { "Taste Mod-Menü", "Mod menu key" } },
            { "OverlayKey", new[] { "Taste FPS-Anzeige", "FPS display key" } },
            { "SaveKey", new[] { "Taste Speichern", "Save key" } },
            { "WeekPlanKey", new[] { "Taste Wochenplan", "Week plan key" } },
            { "HideHudKey", new[] { "Taste HUD ausblenden", "Hide HUD key" } },
            { "DailyReminder", new[] { "Tagesübersicht am Morgen", "Daily reminder" } },
            { "InstantRemove", new[] { "Sofort abbauen", "Instant removal" } },
            { "FullRefund", new[] { "Volle Erstattung beim Abbauen (nicht Vanilla)", "Full refund when removing (not vanilla)" } },
            { "MoveObjects", new[] { "Objekte verschieben (nicht Vanilla)", "Move objects (not vanilla)" } },
            { "TradeLikedAmount", new[] { "Handel: Daumen-hoch-Menge vorschlagen", "Trade: suggest the thumbs-up amount" } },
            { "Respec", new[] { "Talente & Forschung zurückerstatten (nicht Vanilla)", "Refund talents & research (not vanilla)" } },
            { "CraftMax", new[] { "Herstellen: Max-Knopf", "Crafting: Max button" } },
            { "ZombieRename", new[] { "Zombies umbenennen", "Rename zombies" } },
            { "HudClock", new[] { "Tag & Uhrzeit am HUD", "Day & time on HUD" } },
            { "NoTearing", new[] { "Kein Tearing (VSync erzwingen)", "No tearing (force VSync)" } },
            { "HudClockMode", new[] { "HUD-Zeile zeigt", "HUD line shows" } },
            { "HudClock12h", new[] { "12-Stunden-Uhr", "12-hour clock" } },
            { "EscLeavesConversation", new[] { "Gespräch mit Esc/B verlassen", "Leave conversations with Esc/B" } },
            { "ControllerButton", new[] { "Controller: Taste für Pins", "Controller: button for pins" } },
            { "FasterTransitions", new[] { "Schnellere Übergänge", "Faster transitions" } },
            { "LessMemoryCleanup", new[] { "Speicher seltener aufräumen", "Clean up memory less often" } },
            { "RecipeVariants", new[] { "Werkbank & Rezept-Varianten", "Workbench & recipe variants" } },
            { "IngredientTree", new[] { "Zutaten aufklappen", "Expand ingredients" } },
            { "ShowFuel", new[] { "Brennstoff anzeigen", "Show fuel" } },
            { "KeepBackups", new[] { "Backups pro Spielstand", "Backups per save" } },
            { "MinMinutesBetween", new[] { "Mindestabstand", "Minimum interval" } },
            { "CheckForUpdates", new[] { "Nach Updates suchen", "Check for updates" } },
            { "Language", new[] { "Sprache", "Language" } },
            { "Corner", new[] { "Position", "Position" } },
            { "Separator", new[] { "Trennzeichen", "Separator" } },
            { "HudCenter", new[] { "HUD zur Mitte (Ultrawide)", "HUD to the center (ultrawide)" } },
            { "GpuTemp", new[] { "GPU-Temperatur", "GPU temperature" } },
            { "Chests", new[] { "Zählt mit", "Counts" } },
            { "NotifyReady", new[] { "Meldung wenn fertig", "Notify when ready" } },
            { "AutoUnpin", new[] { "Nach Herstellen lösen", "Unpin after crafting" } },
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
            { "RainAmount", "Wie viele Regentropfen gezeichnet werden. Weniger Regen hilft auf schwachen PCs und dem Steam Deck, 0 % schaltet die Partikel ab (das Wetter selbst bleibt)." },
            { "WideRain", "Regen und Schnee über den ganzen Bildschirm – auf Ultrawide-Monitoren und beim Herauszoomen (das Spiel füllt nur einen 16:9-Bereich)." },
            { "OledBlack", "Reines Schwarz statt Dunkelgrau um die Karte herum (z. B. außerhalb der Kirche oder am Levelrand). Gut für OLED-Bildschirme." },
            { "HighContrast", "Stärkerer Kontrast: dunkler Hintergrund für die Pin-Liste, fette und hellere Haben/Brauchen-Zahlen, größere Tooltips." },
            { "Key", "Taste für einen Screenshot (gespeichert in BepInEx/GK2VanillaPlus/Screenshots)." },
            { "Scale", "Auflösungs-Faktor: 2× = doppelte Bildschirmauflösung in jede Richtung (z. B. 3840×2160 aus 1920×1080)." },
            { "HideHud", "Blendet für den Screenshot die Spiel-Oberfläche und die Mod-Anzeigen aus." },
            { "PauseInBackground", "Spiel pausiert, wenn das Fenster nicht im Vordergrund ist (spart Akku und Hitze)." },
            { "NoTearing", "Verhindert Bildrisse (Tearing): VSync bleibt an, auch wenn ein FPS-Limit gesetzt ist (das Spiel schaltet VSync dann ab). Automatisch = an auf Steam Deck / Linux, aus unter Windows." },
            { "HudClock", "Zeigt Tag und Uhrzeit als zweite Zeile in der Gebietsanzeige oben rechts (unter dem Gebietsnamen), im Stil des Spiels." },
            { "HudClockMode", "Was die HUD-Zeile zeigt: Tag und Uhrzeit, Wochentag und Uhrzeit oder nur die Uhrzeit." },
            { "HudClock12h", "Uhrzeit als 12-Stunden-Uhr (5:00 AM) statt 24 Stunden (05:00)." },
            { "ControllerButton", "Controller: Diese Taste im normalen Spiel gedrückt halten steuert die Pin-Liste (nur solange kein Spiel-Fenster offen ist). Feld anklicken und eine beliebige Controller-Taste drücken; Esc = aus." },
            { "EscLeavesConversation", "Im Gespräch wählt Esc bzw. B (Kreis) „Gehen“, wenn es angeboten wird und die Figur fertig gesprochen hat. Ein offenes Mod-Fenster wird zuerst geschlossen." },
            { "FasterTransitions", "Türen und Kartenreisen: kürzere Abblende und Pause (etwa ein Drittel). Zwischensequenzen und Schlafen bleiben unverändert." },
            { "LessMemoryCleanup", "Türen und Kartenreisen: Das Spiel räumt bei jeder Tür den kompletten Speicher auf – das kostet den Großteil der Wartezeit. Mit der Option nur noch alle 10 Minuten oder wenn der Speicher knapp wird (beim Laden immer). Braucht mehr RAM – unter 16 GB nicht empfohlen." },
            { "RecipeVariants", "Unter einem angepinnten Rezept: die Werkbank und, wenn es den Gegenstand auf mehreren Wegen gibt, < > zum Umschalten (nur bekannte Rezepte)." },
            { "IngredientTree", "Zutaten, die man selbst herstellen kann, bekommen ein +, das ihre eigenen Zutaten zeigt (bis 3 Ebenen)." },
            { "ShowFuel", "Zeigt den Brennstoff, den ein Rezept aus der Werkbank braucht (z. B. Ofen), als eigene Zeile." },
            { "FullRefund", "Nicht mehr ganz Vanilla – oft gewünscht, deshalb als Option: Beim Abbauen bekommst du die vollen Baukosten zurück statt nur einen Teil. Inhalt (Inventar, Brennstoff) kommt wie gewohnt zurück." },
            { "MoveObjects", "Nicht mehr ganz Vanilla – oft gewünscht, deshalb als Option: Im Abriss-Modus mit der Dreh-Taste ein Objekt aufnehmen und woanders hinstellen. Es bleibt dasselbe Objekt (Inhalt und Herstell-Warteschlange bleiben), es wird nichts verbraucht oder erstattet. Geht für Werkbänke, Förderbänder und alle anderen Bauten: Zombie-Arbeiter landen auf dem Boden, verbundene Erweiterungen bleiben stehen, bis du sie auch verschiebst. Esc/Rechtsklick bricht ab." },
            { "Respec", "Nicht mehr ganz Vanilla – oft gewünscht, deshalb als Option: Talente, Zombie-Perks und Forschung zurückerstatten. Talente und Zombie-Perks: Rechtsklick auf einen freigeschalteten Knoten (Controller: die Taste, die darunter steht). Forschung: erforschte Technik anklicken → „Zurückerstatten“. Fragt immer vorher; was davon abhängt, wird mit erstattet. Start-Knoten und Ruf-Forschung nie, Effekte beim Kauf (z. B. Gegenstände) bleiben. Verändert den Spielstand – Backups werden automatisch angelegt." },
            { "CraftMax", "Herstellen: Ein Knopf „Max“ neben der Mengenwahl stellt so viele ein, wie deine Zutaten hergeben (Inventar und erreichbare Truhen, wie das Spiel zählt). Am Controller mit der Taste, die auf dem Knopf steht." },
            { "TradeLikedAmount", "Handel mit Stadt-Händlern: Der Mengen-Regler startet genau bei der Menge, die noch Zufriedenheit (Daumen hoch) bringt, und ein Knopf legt alle passenden Waren in der richtigen Menge in den Handel. Bestätigen musst du den Handel weiterhin selbst." },
            { "ZombieRename", "Im Zombie-Fenster steht neben dem Namen ein „Umbenennen“-Knopf: eigenen Namen eintippen oder neu würfeln – jederzeit." },
            { "InstantRemove", "Abriss-Modus beim Bauen: Werkbänke, Truhen, Öfen usw. werden sofort entfernt, statt dass deine Figur erst hinläuft. Du bekommst dieselben Materialien zurück. Hilft bei Objekten, die deine Figur nicht erreicht." },
            { "AutoSaveMinutes", "Zusätzlicher Autosave. Speichert nur, wenn du frei steuerbar bist." },
            { "MainMenuExtend", "Füllt auf Ultrawide-Bildschirmen die Seiten des Hauptmenüs mit einer unscharfen Kopie des Menübilds." },
            { "SkipIntro", "Logos und Intro-Videos beim Spielstart überspringen." },
            { "GameMenuButton", "Zeigt im Hauptmenü und im Pausenmenü (Esc) einen Button \"Vanilla+\", der dieses Menü öffnet." },
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
            { "GpuTemp", "GPU-Temperatur in °C. Nur NVIDIA-Grafikkarten (AMD/Intel und die CPU-Temperatur lassen sich ohne zusätzlichen System-Treiber nicht auslesen). Nicht unter Mac/Linux." },
            { "HudCenter", "Ultrawide: HUD, Gebietsname, NPC-Fenster und die Mod-Anzeigen rücken in den 16:9-Bereich in der Mitte statt an die äußersten Bildschirmränder. Die Spielwelt bleibt ultrabreit." },
            { "Chests", "Was als „Haben“ zählt: Nur Inventar = nur was du dabei hast. Gebiet = dein Inventar und die Truhen im Gebiet, in dem du bist (wie beim Herstellen im Spiel). „Alle Truhen“ = zusätzlich alle Truhen auf der ganzen Karte." },
            { "NotifyReady", "Kurze Meldung mit Ton, wenn für ein angepinntes Rezept alles da ist oder eine angepinnte Quest erledigt ist." },
            { "AutoUnpin", "Ein Rezept automatisch loslösen, sobald du es herstellst." },
            { "Separator", "Trennzeichen zwischen den Werten, wenn sie in einer Zeile stehen. Die Reihenfolge der Werte stellst du unten mit den Pfeilen ein." },
            { "Enabled", "Rezepte, Baupläne und Stadtgebäude über die Pinnadel oben rechts anpinnen. Die Liste zeigt pro Zutat Haben/Brauchen." },
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
            { "Scene", new[] { "Standard (Szene des Spiels)", "Default (game scene)" } },
            { "Bg1", new[] { "Friedhof im Regen", "Graveyard in the rain" } },
            { "Bg2", new[] { "Überflutetes Viertel", "Flooded quarter" } },
            { "Bg3", new[] { "Stadt bei Nacht", "Town at night" } },
            { "FogDark", new[] { "Düster (dunkel)", "Gloomy (dark)" } },
            { "FogLight", new[] { "Hell", "Light mist" } },
            { "Bg4", new[] { "Dein Hof", "Your yard" } },
            { "Bg5", new[] { "Das Dorf", "The village" } },
            { "Bg6", new[] { "Weinberg am Teich", "Vineyard by the pond" } },
            { "Mine", new[] { "Eigenes Bild (Spielstand)", "My picture (from my save)" } },
            { "Natural", new[] { "Natürlich", "Natural" } },
            { "Gloomy", new[] { "Düster", "Gloomy" } },
            { "Sepia", new[] { "Sepia", "Sepia" } },
            { "Night", new[] { "Nacht", "Night" } },
            { "Painting", new[] { "Gemälde", "Painting" } },
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
            { "Português", new[] { "Português", "Português" } },
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
            { "None", new[] { "Keins", "None" } },
            { "Inventory", new[] { "Nur Inventar", "Inventory only" } },
            { "Area", new[] { "Inventar + Truhen im Gebiet", "Inventory + chests in the area" } },
            { "Everywhere", new[] { "Inventar + alle Truhen", "Inventory + all chests" } },
            { "Dash", new[] { "Langer Strich  —", "Long dash  —" } },
            { "Bar", new[] { "Senkrechter Strich  |", "Vertical bar  |" } },
            { "Dot", new[] { "Punkt  ·", "Dot  ·" } },
            { "DayAndTime", new[] { "Tag + Uhrzeit", "Day + time" } },
            { "WeekdayAndTime", new[] { "Wochentag + Uhrzeit", "Weekday + time" } },
            { "TimeOnly", new[] { "Nur Uhrzeit", "Time only" } },
        };

        internal static string Name(ConfigEntryBase e)
        {
            string key = e.Definition.Key;
            if (Names.TryGetValue(e.Definition.Section + "." + key, out string[] sn)) return T(sn[0], sn[1]);
            return Names.TryGetValue(key, out string[] n) ? T(n[0], n[1]) : key;
        }

        internal static string Tip(ConfigEntryBase e)
        {
            if (German && TipsDe.TryGetValue(e.Definition.Section + "." + e.Definition.Key, out string st)) return st;
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
                if (key == "MenuBackgroundBlur") return i == 0 ? T("Aus", "Off") : i == 1 ? T("Leicht", "Light") : i == 2 ? T("Mittel", "Medium") : T("Stark", "Strong");
                if (key == "MenuBackgroundFog") return i == 0 ? T("Aus", "Off") : i == 1 ? T("Leicht", "Light") : i == 2 ? T("Mittel", "Medium") : T("Dicht", "Dense");
                if (key == "MenuBackgroundDim") return i == 0 ? T("Aus", "Off") : i + " %";
                if (key == "RainAmount") return i == 0 ? T("Aus", "Off") : i + " %";
                if (key == "Size") return i + " px";
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
        internal static readonly string[] Shipped = { "fr", "es", "pt", "ru", "zh" };

        // Vorlage fuer eigene Uebersetzungen in den Mod-Ordner schreiben (Steam laesst Dateien mit "_" beim Workshop-Upload weg)
        internal const string TemplateFile = "translation-template.txt";
        internal static void WriteTemplate()
        {
            try
            {
                using (var s = typeof(Translations).Assembly.GetManifestResourceStream("GK2Tweaks.lang.template.txt"))
                {
                    if (s == null) return;
                    string text;
                    using (var r = new System.IO.StreamReader(s, System.Text.Encoding.UTF8)) text = r.ReadToEnd();
                    System.IO.Directory.CreateDirectory(Folder);
                    string f = System.IO.Path.Combine(Folder, TemplateFile);
                    if (!System.IO.File.Exists(f) || System.IO.File.ReadAllText(f, System.Text.Encoding.UTF8) != text)
                        System.IO.File.WriteAllText(f, text, new System.Text.UTF8Encoding(false));
                    string old = System.IO.Path.Combine(Folder, "_template.txt");
                    if (System.IO.File.Exists(old) && new System.IO.FileInfo(old).Length < 64) System.IO.File.Delete(old);
                }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Translation template: " + e.Message); }
        }
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
