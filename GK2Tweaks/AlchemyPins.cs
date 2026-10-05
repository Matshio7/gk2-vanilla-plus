using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2Tweaks
{
    // Alchemie-Rezepte aus dem Folio (Alchemielabor) anpinnen: Nadel an jeder Formel.
    // Alchemie hat keine festen Zutaten - gebraucht wird eine Runen-Summe (rot / gruen / blau) aus beliebigen Zutaten.
    // Der Pin zeigt deshalb die benoetigten Runen als Notiz und keine Habe/Brauche-Zeilen.
    internal static class AlchemyPins
    {
        internal const string Prefix = "alchemy:";

        internal static bool Is(Pins.Pin p) => p?.Key != null && p.Key.StartsWith(Prefix);

        internal static AlchemyFormulaDef Def(string id)
        {
            try { return ((GameBalanceBase)GameBalance.Me).GetData<AlchemyFormulaDef>(id); } catch { return null; }
        }

        internal static string Suffix => " · " + Labels.T("Alchemie", "Alchemy");

        // "Runen: Rot 2 · Blau 1" (farbig, Rich-Text)
        internal static string Note(AlchemyFormulaDef def)
        {
            if (def == null) return "";
            var parts = new List<string>();
            if (def.runesRed > 0) parts.Add("<color=#e06666>" + Labels.T("Rot", "Red") + " " + def.runesRed + "</color>");
            if (def.runesGreen > 0) parts.Add("<color=#7ccf6b>" + Labels.T("Grün", "Green") + " " + def.runesGreen + "</color>");
            if (def.runesBlue > 0) parts.Add("<color=#6fa8f0>" + Labels.T("Blau", "Blue") + " " + def.runesBlue + "</color>");
            if (parts.Count == 0) return "";
            return Labels.T("Runen", "Runes") + ": " + string.Join(" · ", parts.ToArray());
        }

        internal static Pins.Pin Make(AlchemyFormulaDef def)
        {
            string id = ((BalanceBaseObject)def).id;
            string icon = null;
            try { icon = def.ItemDef?.iconId; } catch { }
            string suffix = Suffix;
            return new Pins.Pin { Key = Prefix + id, TitleKey = id, Suffix = suffix, Title = Pins.Loc(id) + suffix, IconId = icon, Note = Note(def) };
        }

        // Beim Laden: Notiz neu aufbauen (Sprache kann gewechselt haben)
        internal static void Restore(Pins.Pin p)
        {
            if (!Is(p)) return;
            p.Note = Note(Def(p.Key.Substring(Prefix.Length)));
        }
    }

    [HarmonyPatch(typeof(UIAlchemyFormulaWidget), nameof(UIAlchemyFormulaWidget.Redraw))]
    internal static class AlchemyPinPatch
    {
        private static void Postfix(UIAlchemyFormulaWidget __instance)
        {
            if (!SafeMode.On("Pins")) return;
            try
            {
                UIAlchemyFormulaWidgetData d = Traverse.Create(__instance).Field("data").GetValue<UIAlchemyFormulaWidgetData>();
                AlchemyFormulaDef def = d?.AlchemyFormulaDef;
                if (!Plugin.PinsEnabled.Value || def == null || d.ItemDef != null) { PinButton.Attach(__instance, null, null); return; }
                PinButton.Attach(__instance, AlchemyPins.Prefix + ((BalanceBaseObject)def).id, () => AlchemyPins.Make(def));
            }
            catch (Exception e) { SafeMode.Fail("Pins", e); }
        }
    }
}
