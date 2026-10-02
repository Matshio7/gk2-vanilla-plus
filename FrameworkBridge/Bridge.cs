using BepInEx.Configuration;
using GK2.Framework;
using GK2Tweaks;

namespace GK2VanillaPlus.FrameworkBridge
{
    // Optionale Anbindung an GK2 Mod Framework (MIT, SuperMan4eg): Vanilla+ erscheint in dessen "Mods"-Menue
    // mit einem Knopf, der das volle Vanilla+-Menue oeffnet, und den wichtigsten Schaltern. Die Schalter sind
    // dieselben Einstellungen wie im Vanilla+-Menue (gleiche Config-Eintraege). Wird nur geladen, wenn das
    // Framework installiert ist (GK2Tweaks.FrameworkBridgeLoader).
    public static class Bridge
    {
        public static void Register()
        {
            FrameworkApi.RegisterMod(new VanillaPlusMod(), VanillaPlusApi.Config);
        }
    }

    internal sealed class VanillaPlusMod : Gk2ModBase
    {
        private readonly Gk2ModMetadata meta = new Gk2ModMetadata(
            "gk2vanillaplus", "GK2 Vanilla+", "McFly7", VanillaPlusApi.Version,
            VanillaPlusApi.T("Ultrawide, Leistung und Komfort. Das komplette Menü öffnet der Knopf unten oder F9.",
                             "Ultrawide, performance and quality of life. The full menu opens with the button below or F9."),
            false, false, false);

        public override Gk2ModMetadata Metadata => meta;

        public override void OnRegister(Gk2ModContext context)
        {
            Gk2Settings s = context.Settings;
            string menu = VanillaPlusApi.T("Vanilla+-Menü", "Vanilla+ menu");

            // "Knopf": ein Schalter, der sofort wieder zurueckspringt und das Vanilla+-Menue oeffnet
            ConfigEntry<bool> open = s.AddToggle(menu, "OpenVanillaPlusMenu", false,
                VanillaPlusApi.T("Vanilla+-Menü öffnen", "Open Vanilla+ menu"),
                VanillaPlusApi.T("Öffnet das komplette Menü von GK2 Vanilla+ (alle Einstellungen).", "Opens the full GK2 Vanilla+ menu (all settings)."), -100);
            open.Value = false;
            open.SettingChanged += (a, b) =>
            {
                if (!open.Value) return;
                VanillaPlusApi.NextFrame(() =>
                {
                    open.Value = false;
                    FrameworkApi.ToggleModsMenu();   // Framework-Fenster schliessen ...
                    VanillaPlusApi.NextFrame(VanillaPlusApi.OpenMenu); // ... dann das Vanilla+-Menue oeffnen
                });
            };
            s.AddReadOnly(menu, "MenuKey", VanillaPlusApi.T("Taste", "Key"), "", () => VanillaPlusApi.MenuKeyText, -99);

            // die wichtigsten Schalter (dieselben Einstellungen wie im Vanilla+-Menue)
            Toggle(s, "Interface", "HudCenter", false, 1);
            Toggle(s, "Interface", "WideRain", true, 2);
            Toggle(s, "Interface", "OledBlack", false, 3);
            Toggle(s, "Interface", "ShowOverlay", false, 4);
            Toggle(s, "Pins", "Enabled", true, 5);
            Toggle(s, "Comfort", "HudClock", true, 6);
            Toggle(s, "Comfort", "EscLeavesConversation", true, 7);
            Toggle(s, "Comfort", "InstantRemove", false, 8);
            Toggle(s, "Performance", "FasterTransitions", true, 9);
        }

        private static void Toggle(Gk2Settings s, string section, string key, bool def, int order)
        {
            s.AddToggle(section, key, def, VanillaPlusApi.Name(section, key), VanillaPlusApi.Tip(section, key), order);
        }
    }
}
