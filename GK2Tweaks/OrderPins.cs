using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2Tweaks
{
    // Stadtauftraege (Haendler-Auftraege) anpinnen: Nadel an jedem Auftrag im Auftragsfenster (Maus oder L3 + R3).
    // Der Pin zeigt die Ware und wie viel noch fehlt (Auftragsmenge minus schon geliefert) - gezaehlt wird wie bei
    // Rezepten (Inventar / Gebiet / alle Truhen). Ist der Auftrag erledigt oder nicht mehr aktiv, verschwindet der Pin.
    // Wunsch von K-Bro (Steam-Diskussionen).
    internal static class OrderPins
    {
        internal const string Prefix = "order:";

        internal static string KeyOf(VendorOrderData od) => Prefix + od.Guid.Guid;

        // Noch zu liefernde Menge (dringende Auftraege werden auf einmal geliefert)
        internal static int Remaining(VendorOrderData od)
        {
            VendorOrderDef def = ((ObjectLinkedToDefinition<VendorOrderDef>)od).Definition;
            int done = def.isUrgent ? 0 : od.Count;
            return Math.Max(0, def.count - done);
        }

        internal static Pins.Pin Make(VendorOrderData od, Vendor v)
        {
            VendorOrderDef def = ((ObjectLinkedToDefinition<VendorOrderDef>)od).Definition;
            string icon = null;
            try { icon = new NeedItemData(def.itemId, 1).ItemDef?.iconId; } catch { }
            string vendorName = null;
            try
            {
                string vid = ((ObjectLinkedToDefinition<VendorDef>)v).id;
                string n = Pins.Loc(vid);
                if (!string.IsNullOrEmpty(n) && n != vid) vendorName = n;
            }
            catch { }
            string suffix = " · " + Labels.T("Auftrag", "Order") + (vendorName != null ? " (" + vendorName + ")" : "");
            var p = new Pins.Pin { Key = KeyOf(od), TitleKey = def.itemId, Suffix = suffix, Title = Pins.Loc(def.itemId) + suffix, IconId = icon };
            p.Needs.Add(new Pins.Need { Id = def.itemId, Count = Math.Max(1, Remaining(od)), IconId = icon, Name = Pins.Loc(def.itemId) });
            return p;
        }

        // Aktiver Auftrag zum Pin-Schluessel (null = erledigt oder nicht mehr angenommen)
        // Auftrag zum Pin-Schluessel: erst die angenommenen, dann alle Auftraege aller Haendler (Auftrags-Liste)
        internal static VendorOrderData Find(string key)
        {
            try
            {
                VendorSystem vs = MainGame.Instance.GameSave.vendorSystem;
                foreach (var t in vs.GetCurrentOrders())
                    if (t.Item1 != null && KeyOf(t.Item1) == key) return t.Item1;
                var vendors = Traverse.Create(vs).Field("vendors").GetValue<List<Vendor>>();
                if (vendors != null)
                    foreach (Vendor v in vendors)
                        if (v?.Orders != null)
                            foreach (VendorOrderData od in v.Orders)
                                if (od != null && KeyOf(od) == key) return od;
            }
            catch { }
            return null;
        }

        // Vor jedem Neuzaehlen: Restmenge nachziehen, erledigte Auftraege abpinnen
        internal static void Update(List<Pins.Pin> list, List<Pins.Pin> remove)
        {
            foreach (Pins.Pin p in list)
            {
                if (p.Key == null || !p.Key.StartsWith(Prefix)) continue;
                VendorOrderData od = Find(p.Key);
                if (od == null || od.State == VendorOrderState.Finished || od.IsFinishedThisWeek) { remove.Add(p); continue; }
                int rest = Remaining(od);
                if (rest <= 0) { remove.Add(p); continue; }
                if (p.Needs.Count > 0) p.Needs[0].Count = rest;
            }
        }
    }

    [HarmonyPatch(typeof(UIVendorOrderWidget), nameof(UIVendorOrderWidget.Redraw))]
    internal static class OrderPinPatch
    {
        private static void Postfix(UIVendorOrderWidget __instance)
        {
            if (!SafeMode.On("Pins")) return;
            try
            {
                UIVendorOrderWidgetData d = __instance.Data;
                VendorOrderData od = d?.VendorOrderData;
                // angenommene Auftraege (Auftragsfenster) und offene Auftraege in der Liste (Lagerhaus "Befehle"), nicht gesperrte Stufen
                bool locked = false;
                try { locked = d != null && d.Vendor != null && od != null && d.Vendor.CurTier < od.Tier; } catch { }
                bool active = od != null && !d.IsEmpty && od.State != VendorOrderState.Finished && !od.IsFinishedThisWeek && !locked;
                if (!Plugin.PinsEnabled.Value || !active) { PinButton.Attach(__instance, null, null, 18f); return; }
                Vendor v = d.Vendor;
                PinButton.Attach(__instance, OrderPins.KeyOf(od), () => OrderPins.Make(od, v), 18f);
            }
            catch (Exception e) { SafeMode.Fail("Pins", e); }
        }
    }
}
