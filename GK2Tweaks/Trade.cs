using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Handel mit Stadt-Haendlern: Fuer Waren mit Daumen-hoch (Zufriedenheit) nimmt der Haendler pro Woche nur eine
    // bestimmte Menge mit Bonus an. Idee aus den Steam-Diskussionen ("Automate trade perfection").
    // - Der Mengen-Regler startet bei genau der Menge, die noch Zufriedenheit bringt (statt bei 1).
    // - Knopf "Daumen-hoch-Waren einlegen": legt von allen passenden Waren genau die passende Menge in den Handel.
    // Es wird nur vorgeschlagen bzw. eingelegt - gehandelt wird erst mit dem normalen Knopf des Spiels.
    internal static class TradeHelper
    {
        internal static int Prefill;
        internal static string PrefillId;

        private static string Id(Item it) => ((ObjectLinkedToDefinition<ItemDef>)it).id;

        // Fenster nur nehmen, wenn das Spiel es schon geladen hat (LazyUI.GetWindow wuerde es sonst neu laden)
        private static System.Collections.IDictionary windowsCache;
        internal static T Cached<T>() where T : class
        {
            if (windowsCache == null) windowsCache = Traverse.Create(typeof(LazyUI)).Field("windowsCache").GetValue<System.Collections.IDictionary>();
            return windowsCache != null && windowsCache.Contains(typeof(T)) ? windowsCache[typeof(T)] as T : null;
        }

        internal static UIVendorWindowData OpenData()
        {
            try
            {
                UIVendorWindow w = TradeHelper.Cached<UIVendorWindow>();
                if (w == null || !w.IsShown) return null;
                var d = Traverse.Create(w).Field("data").GetValue<UIVendorWindowData>();
                if (d == null || d.Vendor == null || !((ObjectLinkedToDefinition<VendorDef>)d.Vendor).Definition.townVendor) return null;
                return d;
            }
            catch { return null; }
        }

        // Menge, die fuer diese Ware noch Zufriedenheit bringt (0 = keine)
        internal static int Best(UIVendorWindowData d, string id, int have)
        {
            if (d == null || string.IsNullOrEmpty(id) || have <= 0) return 0;
            Vendor v = d.Vendor;
            TownVendorProductInfo info = v.CurrentTierData.GetTownVendorProductInfo(id);
            if (info == null || info.perOne <= 0f) return 0;
            int pending = d.GetPendingHappinessSoldCount != null ? d.GetPendingHappinessSoldCount(id) : 0;
            int left = info.itemCount - v.SoldItemsWithHappinessThisWeek.GetInt(id) - Math.Max(0, pending);
            float deal = d.GetTotalHappinessDealDelegate != null ? d.GetTotalHappinessDealDelegate() : 0f;
            float cap = v.CurrentTierData.happinessCap.EvaluateFloat() - v.UsedHappinessThisWeek - deal;
            if (left <= 0 || cap <= 0.0001f) return 0;
            int byCap = Mathf.CeilToInt(cap / info.perOne - 0.0001f);
            return Mathf.Max(0, Mathf.Min(have, Mathf.Min(left, byCap)));
        }

        // Alle Daumen-hoch-Waren in passender Menge in den Handel legen. Rueckgabe: Anzahl eingelegter Gegenstaende
        internal static int FillAll()
        {
            UIVendorWindowData d = OpenData();
            if (d == null || d.GetPendingHappinessSoldCount == null) return 0;
            object trading = d.GetPendingHappinessSoldCount.Target;
            var sell = Traverse.Create(trading).Field("sellInventory").GetValue<Inventory>();
            Inventory player = MainGame.PlayerData.inventory;
            if (sell == null || player == null) return 0;
            var infos = new List<TownVendorProductInfo>(d.Vendor.CurrentTierData.townVendorProductInfos);
            infos.Sort((a, b) => b.perOne.CompareTo(a.perOne)); // meiste Zufriedenheit pro Stueck zuerst
            int moved = 0;
            foreach (TownVendorProductInfo info in infos)
            {
                if (info == null || string.IsNullOrEmpty(info.itemId) || info.Definition == null) continue;
                if (!d.Vendor.CanBuyItemFromPlayer(info.Definition)) continue;
                int have = player.Data.GetTotalCountInInventory(info.itemId);
                int n = Best(d, info.itemId, have);
                if (n <= 0) continue;
                if (sell.AddItemToInventory(new Item(info.itemId, n)))
                {
                    player.RemoveItemById(info.itemId, n);
                    moved += n;
                }
            }
            if (moved > 0)
            {
                d.OnRedraw?.Invoke();
                try { LazyAudio.PlayAndForget("item_put"); } catch { }
            }
            return moved;
        }
    }

    // Klick auf eine Ware im eigenen Inventar -> Mengen-Fenster: Startwert merken
    [HarmonyPatch(typeof(Trading), "OnPlayerItemPress1")]
    internal static class TradePressPatch
    {
        private static void Prefix(Trading __instance, UIItemCell itemCell)
        {
            TradeHelper.Prefill = 0;
            if (!Plugin.TradeLikes.Value) return;
            try
            {
                var d = Traverse.Create(__instance).Field("cachedWindowData").GetValue<UIVendorWindowData>();
                if (d == null || !((ObjectLinkedToDefinition<VendorDef>)d.Vendor).Definition.townVendor) return;
                string id = ((ObjectLinkedToDefinition<ItemDef>)itemCell.DisplayingItem).id;
                TradeHelper.Prefill = TradeHelper.Best(d, id, MainGame.PlayerData.inventory.Data.GetTotalCountInInventory(id));
                TradeHelper.PrefillId = id;
            }
            catch { TradeHelper.Prefill = 0; }
        }

        private static void Postfix() { TradeHelper.Prefill = 0; }
    }

    [HarmonyPatch(typeof(UIItemCountWindow), nameof(UIItemCountWindow.Open))]
    internal static class TradeCountPatch
    {
        private static void Postfix(UIItemCountWindow __instance, UIItemCountWindowData data)
        {
            if (TradeHelper.Prefill <= 0 || data == null || !data.IsForVendor) return;
            try
            {
                if (((ObjectLinkedToDefinition<ItemDef>)data.Item).id != TradeHelper.PrefillId) return;
                var slider = Traverse.Create(__instance).Field("slider").GetValue<SmartSlider>();
                int v = Mathf.Clamp(TradeHelper.Prefill, data.Min, data.Max);
                AccessTools.Method(typeof(SmartSlider), "SetValue").Invoke(slider, new object[] { v, true });
            }
            catch (Exception e) { Plugin.Log.LogWarning("Trade prefill: " + e.Message); }
        }
    }
}
