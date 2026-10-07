using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Herstellen: Knopf "Max" neben dem Plus der Mengenwahl - setzt die Anzahl auf so viele, wie die
    // vorhandenen Zutaten hergeben (gezaehlt wie im Spiel: Inventar + erreichbare Truhen der Werkbank).
    // Zutaten in der Warteschlange werden erst beim Start verbraucht, ein schon laufender Durchgang zaehlt extra.
    // 1.7.1: kein Eingriff mehr in die Tastenleiste des Spiels (Fenster blieben bei manchen Spielern haengen).
    // Controller: Hinweis "[Y] Max" als eigener Text neben dem Plus, die Taste setzt die Menge.
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

        // wie oft das Rezept mit den aktuell gewaehlten Zutaten hergestellt werden kann (-1 = unbekannt / gar nicht).
        // 1.7.2: vorsichtig rechnen - lieber eins zu wenig als zu viel. Ein Auftrag mit mehr Durchgaengen als Zutaten
        // bleibt im Spiel sonst als "wartet" an der Werkbank haengen und blockiert sie (auch ohne Mod im Spielstand).
        // - kein +1 mehr fuer einen schon laufenden Durchgang
        // - Zutaten, die noch nicht gestartete Auftraege in der Warteschlange brauchen, sind reserviert
        // - Werkzeug mit Haltbarkeit (Saege ...): so viele Durchgaenge, wie die Haltbarkeit hergibt
        internal static int Target(UIBaseCraftSelectionWindowData d)
        {
            if (d == null || d.CurrentNeedItems == null || d.WgoData == null) return -1;
            MultiInventory inv = d.WgoData.GetCraftableMultiInventory();
            var reserved = Reserved(d);
            CraftDef def = d.CraftDefinition;
            string toolId = def != null && def.hasDurabilityUseItem && def.durabilityUseItem != null ? def.durabilityUseItem.Id : null;
            int max = int.MaxValue;
            foreach (NeedItemData n in d.CurrentNeedItems)
            {
                if (n == null || string.IsNullOrEmpty(n.Id) || n.Id == toolId) continue;
                int per = n.GetCount(d.WgoData);
                if (per <= 0) continue;
                reserved.TryGetValue(n.Id, out int r);
                max = Math.Min(max, Math.Max(0, inv.GetTotalCount(n.Id) - r) / per);
            }
            if (toolId != null) max = Math.Min(max, ToolUses(d, toolId, def.needItemsDurabilityUse));
            if (max == int.MaxValue || max < 1) return -1;
            return max;
        }

        private static Dictionary<string, int> Reserved(UIBaseCraftSelectionWindowData d)
        {
            var res = new Dictionary<string, int>();
            try
            {
                if (d.CraftQueue == null) return res;
                foreach (CraftElementBase e in d.CraftQueue)
                {
                    if (e == null || e.IsStarted || e.Requirements == null || e.Count <= 0) continue;
                    foreach (NeedItemData r in e.Requirements)
                    {
                        if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                        res.TryGetValue(r.Id, out int have);
                        res[r.Id] = have + r.GetCount(d.WgoData) * e.Count;
                    }
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("CraftMax reserved: " + ex.Message); }
            return res;
        }

        // Durchgaenge, die die Werkzeuge im Inventar des Arbeiters (Spieler) noch schaffen
        private static int ToolUses(UIBaseCraftSelectionWindowData d, string toolId, float need)
        {
            if (need <= 0f) return int.MaxValue;
            Inventory inv = null;
            try { inv = d.WgoData.CraftableAttachedWorker?.WorkerInventory; } catch { }
            if (inv == null) try { inv = MainGame.PlayerData.Inventory; } catch { }
            if (inv?.Data?.Inventory == null) return int.MaxValue;
            int uses = 0;
            foreach (Item it in inv.Data.Inventory)
            {
                if (it == null || ((ObjectLinkedToDefinition<ItemDef>)it).id != toolId) continue;
                if (it.TryGetProperty<DurabilitySerializedItemProperty>(out var p)) uses += (int)Math.Floor(p.Durability / need + 1e-4f);
            }
            return uses;
        }

        // true = Anzahl geaendert
        internal static bool Apply(UIBaseCraftSelectionWindow w)
        {
            UIBaseCraftSelectionWindowData d = Data(w);
            int t = Target(d);
            if (t < 1) return false;
            if (d.CraftsCount > t) t = d.CraftsCount; // nur erhoehen, nie die eigene Eingabe kleiner machen
            int delta = t - d.CraftsCount;
            if (delta == 0) return false;
            Traverse.Create(w).Method("ChangeCraftCount", new[] { typeof(int) }).GetValue(delta);
            try { LazyAudio.PlayAndForget("gui_click"); } catch { }
            return true;
        }
    }
}
