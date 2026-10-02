using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2Tweaks
{
    // Pin-Liste 2.0: Rezept-Varianten (gleicher Gegenstand, andere Werkbank/Zutaten) mit < > umschalten,
    // Zutaten, die man selbst herstellt, mit + aufklappen (bis 3 Ebenen), Brennstoff-Zeile, Eintraege einklappen.
    // Nur bekannte (freigeschaltete) Rezepte werden angezeigt - keine Spoiler.
    internal static class Recipes
    {
        private static Dictionary<string, List<CraftDef>> byOutput;
        private static Dictionary<string, CraftDef> byId;

        private static void Ensure()
        {
            if (byOutput != null) return;
            byOutput = new Dictionary<string, List<CraftDef>>();
            byId = new Dictionary<string, CraftDef>();
            try
            {
                foreach (CraftDef d in GameBalance.Me.craftDefs)
                {
                    if (d == null || string.IsNullOrEmpty(d.id)) continue;
                    byId[d.id] = d;
                    if (d.isHidden || d.isFuelCraft || d.isAutopsyCraft || d.isPocketExtractCraft) continue;
                    if (d.needItems == null || d.needItems.Count == 0) continue;
                    string o = OutputOf(d);
                    if (string.IsNullOrEmpty(o)) continue;
                    if (!byOutput.TryGetValue(o, out List<CraftDef> l)) byOutput[o] = l = new List<CraftDef>();
                    l.Add(d);
                }
                Plugin.Log.LogInfo("Pin recipes: " + byId.Count + " crafts, " + byOutput.Count + " craftable items");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pin recipes: " + e.Message); }
        }

        internal static CraftDef Def(string id)
        {
            Ensure();
            return id != null && byId.TryGetValue(id, out CraftDef d) ? d : null;
        }

        internal static string OutputOf(CraftDef d)
        {
            try { return d.GetOutputPreview(null)?.itemId; } catch { return null; }
        }

        internal static int OutCount(CraftDef d)
        {
            try { OutputPreview p = d.GetOutputPreview(null); return p != null ? Math.Max(1, p.count) : 1; } catch { return 1; }
        }

        internal static bool Known(CraftDef d)
        {
            if (d == null) return false;
            if (!d.isNeedsUnlock) return true;
            try { return MainGame.Instance.GameSave.knowledgeSystem.unlockedCrafts.Contains(d.id); } catch { return true; }
        }

        // alle bekannten Rezepte, die diesen Gegenstand herstellen
        internal static List<CraftDef> For(string itemId)
        {
            Ensure();
            var r = new List<CraftDef>();
            if (itemId != null && byOutput.TryGetValue(itemId, out List<CraftDef> l))
                foreach (CraftDef d in l) if (Known(d)) r.Add(d);
            return r;
        }

        // Varianten eines Rezepts (enthaelt immer das Rezept selbst)
        internal static List<CraftDef> Variants(CraftDef def)
        {
            List<CraftDef> v = For(OutputOf(def));
            if (!v.Contains(def)) v.Insert(0, def);
            return v;
        }

        internal static string Station(CraftDef d)
        {
            try
            {
                if (d.craftsIn == null || d.craftsIn.Count == 0) return null;
                string id = d.craftsIn[0], n = Pins.Loc(id);
                // keine Uebersetzung gefunden -> lieber nichts als eine interne ID
                if (string.IsNullOrEmpty(n) || n == id || (n.IndexOf(' ') < 0 && n.IndexOf('_') >= 0)) return null;
                return n;
            }
            catch { return null; }
        }

        // Brennstoff aus der Werkbank (z. B. Ofen): Name, Icon, Menge
        internal static bool Fuel(CraftDef d, out string name, out string icon, out int count)
        {
            name = icon = null; count = 0;
            try
            {
                if (d.needItemsFromWgo == null) return false;
                foreach (NeedItemData n in d.needItemsFromWgo)
                {
                    ItemDef def = n?.ItemDef;
                    if (def == null || !def.isFuel) continue;
                    name = Pins.Loc(n.Id); icon = def.iconId; count = n.GetCount(null);
                    return count > 0;
                }
            }
            catch { }
            return false;
        }
    }

    internal sealed class PinRow
    {
        public int Depth;
        public string Path, Id, Name, IconId;
        public int Have, Count;
        public bool Expandable, Expanded, Info, Group;
    }

    internal static class PinTree
    {
        private const int MaxDepth = 2; // 0 = Zutaten des Pins, darunter zwei weitere Ebenen

        internal static bool Enabled => Plugin.PinsVariants.Value || Plugin.PinsTree.Value || Plugin.PinsFuel.Value;

        // Kopfzeile unter dem Titel ("1/2 · Kreissaege ×4") und Zeilen fuer die Liste berechnen
        internal static void Build(Pins.Pin p, Func<string, bool, int> have)
        {
            p.Rows.Clear();
            p.VarIndex = p.VarCount = 0; p.Station = null; p.OutCount = 1;
            p.FuelName = p.FuelIcon = null; p.FuelCount = 0;
            if (p.QuestId != null) return;
            CraftDef def = p.Key != null && p.Key.StartsWith("craft:") ? Recipes.Def(p.Key.Substring(6)) : null;
            if (def != null)
            {
                if (Plugin.PinsVariants.Value)
                {
                    List<CraftDef> v = Recipes.Variants(def);
                    p.VarCount = v.Count;
                    p.VarIndex = Math.Max(0, v.IndexOf(def));
                    p.Station = Recipes.Station(def);
                    p.OutCount = Recipes.OutCount(def);
                }
                if (Plugin.PinsFuel.Value && Recipes.Fuel(def, out string fn, out string fi, out int fc))
                { p.FuelName = fn; p.FuelIcon = fi; p.FuelCount = fc * Math.Max(1, p.Mult); }
            }
            var chain = new HashSet<string>();
            string output = def != null ? Recipes.OutputOf(def) : null;
            if (output != null) chain.Add(output);
            foreach (Pins.Need n in p.Needs)
                Add(p, 0, n.Id, n.Id, n.Name, n.IconId, n.Have, n.Count, n.Group, chain, have);
        }

        private static void Add(Pins.Pin p, int depth, string path, string id, string name, string icon, int haveN, int count, bool group,
            HashSet<string> chain, Func<string, bool, int> have)
        {
            var r = new PinRow { Depth = depth, Path = path, Id = id, Name = name, IconId = icon, Have = haveN, Count = count, Group = group };
            p.Rows.Add(r);
            if (!Plugin.PinsTree.Value || group || depth >= MaxDepth || chain.Contains(id)) return;
            List<CraftDef> subs = Recipes.For(id);
            if (subs.Count == 0) return;
            r.Expandable = true;
            r.Expanded = p.Open.Contains(path);
            if (!r.Expanded) return;
            CraftDef sub = subs[0];
            int crafts = Mathf.CeilToInt(Math.Max(1, count) / (float)Recipes.OutCount(sub));
            string st = Recipes.Station(sub);
            p.Rows.Add(new PinRow { Depth = depth + 1, Info = true, Name = (st ?? Labels.T("Herstellen", "Craft")) + (crafts > 1 ? "  ×" + crafts : "") });
            chain.Add(id);
            var seen = new HashSet<string>();
            foreach (NeedItemData n in sub.needItems)
            {
                if (n == null || string.IsNullOrEmpty(n.Id) || !seen.Add(n.Id)) continue;
                int c;
                try { c = n.GetCount(null) * crafts; } catch { c = crafts; }
                string ic = null;
                try
                {
                    if (!n.IsGroup) ic = n.ItemDef?.iconId;
                    else if (n.TryGetGroupItemDefs(out List<ItemDef> defs) && defs != null && defs.Count > 0) ic = defs[0].iconId;
                }
                catch { }
                Add(p, depth + 1, path + ">" + n.Id, n.Id, Pins.Loc(n.Id), ic, have(n.Id, n.IsGroup), c, n.IsGroup, chain, have);
            }
            chain.Remove(id);
        }

        // < > : zur naechsten/vorigen Rezept-Variante wechseln
        internal static void Switch(Pins.Pin p, int dir)
        {
            if (p == null || p.Key == null || !p.Key.StartsWith("craft:")) return;
            CraftDef def = Recipes.Def(p.Key.Substring(6));
            if (def == null) return;
            List<CraftDef> v = Recipes.Variants(def);
            if (v.Count < 2) return;
            int i = Math.Max(0, v.IndexOf(def));
            CraftDef nd = v[(i + dir + v.Count) % v.Count];
            Pins.Pin np = Pins.FromCraftDef(nd, null);
            int mult = Math.Max(1, p.Mult);
            foreach (Pins.Need n in np.Needs) n.Count *= mult;
            p.Key = np.Key; p.TitleKey = np.TitleKey; p.Title = np.Title + p.Suffix;
            if (!string.IsNullOrEmpty(np.IconId)) p.IconId = np.IconId;
            p.Needs = np.Needs;
            p.Open.Clear();
            p.Checked = false;
            Plugin.Log.LogInfo("Pin variant: " + def.id + " -> " + nd.id);
            Pins.AfterEdit();
        }

        internal static void ToggleOpen(Pins.Pin p, string path)
        {
            if (p == null || path == null) return;
            if (!p.Open.Remove(path)) p.Open.Add(path);
            Pins.AfterEdit();
        }

        private static readonly int[] mults = { 1, 2, 3, 4, 5, 10 };

        // Rezept mehrfach herstellen: alle Mengen auf das n-fache
        internal static void StepMult(Pins.Pin p, int dir)
        {
            if (p == null || p.QuestId != null) return;
            int old = Math.Max(1, p.Mult);
            int i = Array.IndexOf(mults, old);
            if (i < 0) i = 0;
            int nm = mults[(i + dir + mults.Length) % mults.Length];
            if (nm == old) return;
            foreach (Pins.Need n in p.Needs) n.Count = Math.Max(1, n.Count / old) * nm;
            p.Mult = nm;
            p.Checked = false;
            Pins.AfterEdit();
        }

        internal static void ToggleCollapsed(Pins.Pin p)
        {
            if (p == null) return;
            p.Collapsed = !p.Collapsed;
            Pins.AfterEdit();
        }
    }
}
