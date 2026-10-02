using UnityEngine;

namespace GK2Tweaks
{
    // Ein-Klick-Profile: setzen mehrere Grafik-/Bildraten-Einstellungen auf einmal.
    // Alles bleibt danach einzeln im Mod-Menue aenderbar; "Spiel-Standard" nimmt alle Eingriffe zurueck.
    internal static class Profiles
    {
        internal static readonly string[] Ids = { "Deck", "Performance", "Quality", "Default" };

        internal static string Name(string id)
        {
            switch (id)
            {
                case "Deck": return Labels.T("Steam Deck / Akku", "Steam Deck / battery");
                case "Performance": return Labels.T("Leistung", "Performance");
                case "Quality": return Labels.T("Qualität", "Quality");
                default: return Labels.T("Spiel-Standard", "Game default");
            }
        }

        internal static string Tip(string id)
        {
            switch (id)
            {
                case "Deck": return Labels.T("Für Steam Deck, Handhelds und Laptops im Akkubetrieb: halbe Auflösung für die Welt, einfache Schatten, keine Umgebungsverdeckung, VSync ohne Tearing, Physik mit 30 Hz. Für 40 FPS am Deck das Bildraten-Limit im Steam-Schnellmenü (…) nutzen – das stellt auch das Display auf 40 Hz.",
                    "For Steam Deck, handhelds and laptops on battery: world at half resolution, simple shadows, no ambient occlusion, VSync without tearing, physics at 30 Hz. For 40 FPS on the Deck use the frame limit in Steam's quick access menu (…) – it also sets the display to 40 Hz.");
                case "Performance": return Labels.T("Für schwächere PCs: volle Auflösung, einfachere Schatten und Lichter, keine Umgebungsverdeckung, stabile 60 FPS.",
                    "For weaker PCs: full resolution, simpler shadows and lights, no ambient occlusion, a steady 60 FPS.");
                case "Quality": return Labels.T("Alles auf höchster Stufe (weiche PC-Schatten, beste Umgebungsverdeckung, echtes Licht), VSync mit voller Bildwiederholrate.",
                    "Everything at the highest level (soft PC shadows, best ambient occlusion, real lights), VSync at the full refresh rate.");
                default: return Labels.T("Nimmt alle Grafik- und Bildraten-Eingriffe des Mods zurück – es gilt wieder nur die Grafikstufe aus dem Spielmenü.",
                    "Removes all graphics and frame rate overrides of the mod – only the graphics tier from the game menu applies.");
            }
        }

        internal static void Apply(string id)
        {
            const string K = Plugin.Keep;
            switch (id)
            {
                case "Deck":
                    Set("Lightweight", "Unity_Low", "Off", "Faked", "Off", "Light", "Replaced", "VSync", 0, 30); Plugin.NoTearing.Value = "Auto"; Plugin.LessMemoryCleanup.Value = false; break;
                case "Performance":
                    Set("Native", "Unity_Balanced", "Off", "Faked", "On", "Light", "Replaced", "Limit", 60, 0); break;
                case "Quality":
                    Set("Native", "NGSS_High", "Highest", "Realtime", "On", "High", "Normal", "VSync", 0, 0); break;
                default:
                    Set(K, K, K, K, K, K, K, "Game", 60, 0); break;
            }
            ManualSave.Toast(string.Format(Labels.T("Profil „{0}“ angewendet.", "Profile \"{0}\" applied."), Name(id)), 3f);
            Plugin.Log.LogInfo("Profile applied: " + id);
        }

        private static void Set(string render, string shadows, string ao, string lights, string back, string water, string clouds, string pacing, int fps, int physics)
        {
            Plugin.RenderMode.Value = render;
            Plugin.Shadows.Value = shadows;
            Plugin.Hbao.Value = ao;
            Plugin.PointLights.Value = lights;
            Plugin.BackLight.Value = back;
            Plugin.Water.Value = water;
            Plugin.Clouds.Value = clouds;
            Plugin.Pacing.Value = pacing;
            Plugin.TargetFps.Value = fps;
            Plugin.PhysicsHz.Value = physics;
        }
    }
}
