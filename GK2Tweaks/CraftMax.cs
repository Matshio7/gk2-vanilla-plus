using System;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Herstellen: Knopf "Max" neben dem Plus der Mengenwahl - setzt die Anzahl auf so viele, wie die
    // vorhandenen Zutaten hergeben (gezaehlt wie im Spiel: Inventar + erreichbare Truhen der Werkbank).
    // Zutaten in der Warteschlange werden erst beim Start verbraucht, ein schon laufender Durchgang zaehlt extra.
    // Controller: "Max" erscheint wie "+" und "-" in der Tastenleiste des Fensters (mit dem Tastensymbol des Spiels)
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "AddCraftCountGamepadTips")]
    internal static class CraftMaxTipPatch
    {
        private static void Postfix(UIBaseCraftSelectionWindow __instance, System.Collections.Generic.List<LazyGameKeyTip> tips)
        {
            if (tips == null || !Plugin.CraftMaxButton.Value || !SafeMode.On("CraftMax")) return;
            try
            {
                if (CraftMax.PlusAnchor(__instance) == null) return;
                GameKey k = PadExtra.KeyFor(__instance);
                if (k == null) return;
                var d = CraftMax.Data(__instance);
                int t = CraftMax.Target(d);
                tips.Add(new LazyGameKeyTip(k, "Max", t > 0 && d != null && d.CraftsCount != t, true, false));
            }
            catch (Exception e) { SafeMode.Fail("CraftMax", e); }
        }
    }

    internal static class CraftMax
    {
        // offenes Herstell-Fenster (Werkbank, Brennstoff, Einzel-Herstellung) oder null
        internal static UIBaseCraftSelectionWindow Open()
        {
            UIBaseCraftSelectionWindow w = Shown(TradeHelper.Cached<UISingleCraftWindow>());
            if (w == null) w = Shown(TradeHelper.Cached<UICraftSelectionWindow>());
            if (w == null) w = Shown(TradeHelper.Cached<UIFuelCraftWindow>());
            return w;
        }

        private static UIBaseCraftSelectionWindow Shown(UIBaseCraftSelectionWindow w)
        {
            try { return w != null && w.IsShown && ((Component)w).gameObject.activeInHierarchy ? w : null; } catch { return null; }
        }

        internal static UIBaseCraftSelectionWindowData Data(UIBaseCraftSelectionWindow w) =>
            w == null ? null : Traverse.Create(w).Field("data").GetValue<UIBaseCraftSelectionWindowData>();

        // Plus-Knopf als Anker (nur wenn sichtbar = Mengenwahl aktiv)
        internal static RectTransform PlusAnchor(UIBaseCraftSelectionWindow w)
        {
            var b = Traverse.Create(w).Field("plusCraftButton").GetValue<Component>();
            return b != null && b.gameObject.activeInHierarchy ? b.transform as RectTransform : null;
        }

        // wie oft das Rezept mit den aktuell gewaehlten Zutaten hergestellt werden kann (-1 = unbekannt)
        internal static int Target(UIBaseCraftSelectionWindowData d)
        {
            if (d == null || d.CurrentNeedItems == null || d.WgoData == null) return -1;
            MultiInventory inv = d.WgoData.GetCraftableMultiInventory();
            int max = int.MaxValue;
            foreach (NeedItemData n in d.CurrentNeedItems)
            {
                if (n == null || string.IsNullOrEmpty(n.Id)) continue;
                int per = n.GetCount(d.WgoData);
                if (per <= 0) continue;
                max = Math.Min(max, inv.GetTotalCount(n.Id) / per);
            }
            if (max == int.MaxValue) return -1;
            bool started = d.CraftQueue != null && d.CraftQueue.Count > 0 && d.CraftQueue[0].IsStarted;
            return Math.Max(1, max + (started ? 1 : 0));
        }

        // true = Anzahl geaendert
        internal static bool Apply(UIBaseCraftSelectionWindow w)
        {
            UIBaseCraftSelectionWindowData d = Data(w);
            int t = Target(d);
            if (t < 1) return false;
            int delta = t - d.CraftsCount;
            if (delta == 0) return false;
            Traverse.Create(w).Method("ChangeCraftCount", new[] { typeof(int) }).GetValue(delta);
            try { Traverse.Create(w).Method("PrintTips").GetValue(); } catch { }
            try { LazyAudio.PlayAndForget("gui_click"); } catch { }
            return true;
        }
    }
}
