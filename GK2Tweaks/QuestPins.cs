using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Quest-Pins: benoetigte Gegenstaende (und Geld) des aktuellen Quest-Schritts mit Haben/Brauchen (Wunsch von Ydyp).
    // Das Repo kennt keine Schritt-/Ziel-Member von QuestData -> begrenzte, gecachte Reflection ueber den Objektgraphen
    // der Quest. Bekannte Blaetter (NeedItemData, GameRes) werden mit ihrer bekannten API gelesen, sonst ueber
    // Namensmuster. Findet sich nichts oder ist es mehrdeutig: keine Zeilen, der Pin ist genau wie in 1.7.6.
    // Opt-in (Standard aus), eigener SafeMode-Name "QuestItems" - ein Fehler schaltet nicht die ganzen Pins ab.
    internal static class QuestPins
    {
        internal const string Feature = "QuestItems";
        internal const string MoneyId = "$money";
        internal static ConfigEntry<bool> Enabled;

        internal static void BindConfig(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Pins", "QuestItems", false, "Quest pins also list the items (and money) the current quest step asks for, with have/need counts like recipe pins. Experimental: read from the game's quest data; if it can't be read, quest pins show only the task text as before.");
        }

        internal static bool Active => Enabled != null && Enabled.Value && SafeMode.On(Feature);

        // ---------- Zustand je Pin ----------
        private sealed class QState { public string Sig, Summary; public float NextScan; public bool Done; }
        private static readonly Dictionary<string, QState> states = new Dictionary<string, QState>();

        private static QState State(Pins.Pin p)
        {
            QState s;
            if (!states.TryGetValue(p.Key, out s)) { s = new QState(); states[p.Key] = s; }
            return s;
        }

        internal static bool Done(Pins.Pin p)
        {
            QState s;
            return p != null && p.Key != null && states.TryGetValue(p.Key, out s) && s.Done;
        }

        internal static void SetDone(Pins.Pin p, bool d)
        {
            if (p == null || p.Key == null) return;
            State(p).Done = d;
        }

        // zurueck auf den Pin von 1.7.6: nur Aufgabentext, keine Zeilen (Done bleibt, das gehoert zur Quest selbst)
        internal static void Reset(Pins.Pin p)
        {
            if (p == null) return;
            p.Needs.Clear();
            QState s;
            if (p.Key != null && states.TryGetValue(p.Key, out s)) { s.Sig = null; s.Summary = null; s.NextScan = 0f; }
        }

        private sealed class Req { public string Id; public bool Group; public int Count; }

        // baut p.Needs bei Bedarf neu, zaehlt Haben; true = es gibt Zeilen und alle sind erfuellt
        internal static bool Update(Pins.Pin p, Func<string, bool, int> have)
        {
            QuestData q = null;
            try { q = MainGame.Instance.GameSave.questSystemData.questCollection.questsCache[p.QuestId]; } catch { }
            if (q == null) { Reset(p); return false; }
            DumpOnce();
            QState st = State(p);
            float now = Time.realtimeSinceStartup;
            string desc = "";
            try { desc = q.Description ?? ""; } catch { }
            string sig = (int)q.status + "|" + desc;
            if (sig != st.Sig || now >= st.NextScan)
            {
                st.Sig = sig;
                st.NextScan = now + 5f;
                // Aufgabentext nach Schrittwechsel nachziehen (bisher nur beim Anpinnen/Laden); leer -> alten Text behalten
                if (!string.IsNullOrEmpty(desc)) p.Note = Pins.Clean(desc);
                Rebuild(p, q, st);
            }
            bool ready = p.Needs.Count > 0;
            for (int i = p.Needs.Count - 1; i >= 0; i--)
            {
                Pins.Need n = p.Needs[i];
                if (n.Id == MoneyId)
                {
                    int m = MoneyHave();
                    if (m < 0) { p.Needs.RemoveAt(i); continue; }   // Geld des Spielers nicht lesbar -> Zeile weg
                    n.Have = m;
                }
                else n.Have = have(n.Id, n.Group);
                if (n.Have < n.Count) ready = false;
            }
            return ready && p.Needs.Count > 0;
        }

        private static void Rebuild(Pins.Pin p, QuestData q, QState st)
        {
            List<Req> reqs = Extract(q);
            var needs = new List<Pins.Need>();
            foreach (Req r in reqs)
            {
                if (r.Count <= 0 || string.IsNullOrEmpty(r.Id)) continue;
                Pins.Need same = needs.Find(x => x.Id == r.Id);
                if (same != null) { same.Count += r.Count; continue; }
                var n = new Pins.Need { Id = r.Id, Group = r.Group, Count = r.Count };
                if (r.Id == MoneyId) n.Name = Labels.T("Geld", "Money");
                else { n.Name = Pins.Loc(r.Id); n.IconId = IconOf(r.Id, r.Group); }
                needs.Add(n);
            }
            if (needs.Exists(x => x.Id == MoneyId) && MoneyHave() < 0) needs.RemoveAll(x => x.Id == MoneyId);
            p.Needs.Clear();
            p.Needs.AddRange(needs);
            var sb = new StringBuilder();
            foreach (Pins.Need n in needs) { if (sb.Length > 0) sb.Append(", "); sb.Append(n.Id).Append('x').Append(n.Count); }
            string summary = sb.ToString();
            if (summary != st.Summary)
            {
                st.Summary = summary;
                if (needs.Count > 0) Plugin.Log.LogInfo("[QuestItems] quest " + p.QuestId + ": " + needs.Count + " requirements (" + summary + ")");
                else Plugin.Log.LogInfo("[QuestItems] no requirement shape found for " + p.QuestId + " - pin stays text-only");
            }
        }

        private static string IconOf(string id, bool group)
        {
            try
            {
                var n = new NeedItemData(id, 1);
                if (!group) return n.ItemDef?.iconId;
                List<ItemDef> defs;
                if (n.TryGetGroupItemDefs(out defs) && defs != null && defs.Count > 0) return defs[0].iconId;
            }
            catch { }
            return null;
        }

        // ---------- Suche im Objektgraphen ----------
        private const int MaxDepth = 4, MaxVisits = 400, MaxElems = 32;
        private static readonly Regex FollowRx = new Regex("step|stage|phase|task|objective|goal|condition|requirement|require|need|deliver|cost|price|item|current|active|def|data|info", RegexOptions.IgnoreCase);
        private static readonly Regex DenyRx = new Regex("reward|prize|bonus|give|grant|receive|gain|unlock|result|output|oncomplete|after|next|prev|parent|child|quests?$", RegexOptions.IgnoreCase);
        private static readonly Regex StepIdxRx = new Regex("(cur|current|active).*(index|idx|step|stage)|^(step|stage)(index|idx)?$", RegexOptions.IgnoreCase);
        private static readonly Regex StepRx = new Regex("step|stage|phase", RegexOptions.IgnoreCase);
        private static readonly Regex DoneRx = new Regex("complete|done|finished|passed", RegexOptions.IgnoreCase);
        private static readonly Regex ActiveRx = new Regex("^(is)?active$|started", RegexOptions.IgnoreCase);
        private static readonly Regex IdRx = new Regex("^_?(item|itemid|item_id|itemdef|resid|res|id)$", RegexOptions.IgnoreCase);
        private static readonly Regex CountRx = new Regex("count|amount|quantity|qty|num|need|value", RegexOptions.IgnoreCase);
        private static readonly Regex DeliveredRx = new Regex("delivered|given|progress|current|done", RegexOptions.IgnoreCase);
        // nur ganze Namen: gold_ore, silver_bar, coin_* oder ein "itemPrice" sind keine Geld-Anforderung
        private static readonly Regex MoneyRx = new Regex("^_?(money|coins?|silver|price|cost)$", RegexOptions.IgnoreCase);
        private static readonly Regex MoneyKeyRx = new Regex("^(money|coins?|silver|gold)$", RegexOptions.IgnoreCase);
        private static readonly Regex WalletRx = new Regex("res|money|wallet|currency", RegexOptions.IgnoreCase);
        private static readonly Regex ReqPathRx = new Regex("require|need|cost|price|deliver|condition|objective|goal|task", RegexOptions.IgnoreCase);
        private static readonly string[] DefNames = { "Definition", "Def", "def", "questDef", "Data" };

        private sealed class M
        {
            public string Name;
            public Type Type;
            public FieldInfo F;
            public PropertyInfo P;
            public object Get(object o)
            {
                try { return F != null ? F.GetValue(o) : P.GetValue(o, null); } catch { return null; }
            }
        }

        // Form eines Typs (gecacht): verfolgte Member und Blatt-Erkennung
        private sealed class Shape
        {
            public M[] All, Follow;
            public M Id, IdDef, Count, Delivered, Money, StepIdx, DoneFlag, ActiveFlag;
            public bool StepType;
        }

        private static readonly Dictionary<Type, Shape> shapes = new Dictionary<Type, Shape>();
        private static readonly HashSet<string> loggedLeaf = new HashSet<string>();

        private sealed class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        private static string Clean(string n)
        {
            // Auto-Property-Hintergrundfeld "<Name>k__BackingField" -> "Name"
            if (n != null && n.StartsWith("<")) { int e = n.IndexOf('>'); if (e > 1) return n.Substring(1, e - 1); }
            return n;
        }

        private static bool IsNum(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(short);

        private static double Num(object v)
        {
            if (v is int) return (int)v;
            if (v is long) return (long)v;
            if (v is float) return (float)v;
            if (v is double) return (double)v;
            if (v is short) return (short)v;
            return double.NaN;
        }

        // Werte, in die nie hineingeschaut wird
        private static bool Skip(Type t)
        {
            if (t == null || t.IsPrimitive || t.IsEnum || t.IsPointer || t == typeof(string) || t == typeof(decimal)) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t) || typeof(Delegate).IsAssignableFrom(t) || typeof(Type).IsAssignableFrom(t) || typeof(MemberInfo).IsAssignableFrom(t)) return true;
            string ns = t.Namespace ?? "";
            if ((ns == "System" || ns.StartsWith("System.")) && !typeof(IEnumerable).IsAssignableFrom(t)) return true;
            if (ns.StartsWith("UnityEngine")) return true;
            // Definitionen (Gegenstand/Rezept) sind keine Anforderungen; ItemDef nur ueber Shape.IdDef lesen
            if (typeof(ItemDef).IsAssignableFrom(t) || typeof(CraftDef).IsAssignableFrom(t)) return true;
            return false;
        }

        private static Shape ShapeOf(Type t)
        {
            Shape s;
            if (shapes.TryGetValue(t, out s)) return s;
            var all = new List<M>();
            var seen = new HashSet<string>();
            for (Type b = t; b != null && b != typeof(object); b = b.BaseType)
            {
                foreach (FieldInfo f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    string n = Clean(f.Name);
                    if (seen.Add(n)) all.Add(new M { Name = n, Type = f.FieldType, F = f });
                }
            }
            foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!pi.CanRead || pi.GetIndexParameters().Length > 0 || pi.GetGetMethod() == null) continue;
                // Properties koennen rechnen -> nur mit passendem Namen, Felder haben Vorrang
                if (!FollowRx.IsMatch(pi.Name) && !IdRx.IsMatch(pi.Name) && !CountRx.IsMatch(pi.Name) && !StepIdxRx.IsMatch(pi.Name)) continue;
                if (seen.Add(pi.Name)) all.Add(new M { Name = pi.Name, Type = pi.PropertyType, P = pi });
            }
            s = new Shape { All = all.ToArray() };
            var follow = new List<M>();
            foreach (M m in all)
            {
                Type mt = m.Type;
                if (s.Id == null && mt == typeof(string) && IdRx.IsMatch(m.Name)) s.Id = m;
                else if (s.IdDef == null && mt == typeof(ItemDef)) s.IdDef = m;
                else if (IsNum(mt))
                {
                    if (s.StepIdx == null && (mt == typeof(int)) && StepIdxRx.IsMatch(m.Name)) s.StepIdx = m;
                    else if (s.Delivered == null && DeliveredRx.IsMatch(m.Name)) s.Delivered = m;
                    else if (s.Count == null && CountRx.IsMatch(m.Name)) s.Count = m;
                    if (s.Money == null && MoneyRx.IsMatch(m.Name)) s.Money = m;
                }
                else if (mt == typeof(bool))
                {
                    if (s.DoneFlag == null && DoneRx.IsMatch(m.Name)) s.DoneFlag = m;
                    if (s.ActiveFlag == null && ActiveRx.IsMatch(m.Name)) s.ActiveFlag = m;
                }
                if (Skip(mt) && !typeof(IEnumerable).IsAssignableFrom(mt)) continue;
                if (mt == typeof(string)) continue;
                if (typeof(QuestData).IsAssignableFrom(mt)) continue;   // andere Quests nie verfolgen
                if (DenyRx.IsMatch(m.Name)) continue;
                if (FollowRx.IsMatch(m.Name) || FollowRx.IsMatch(mt.Name)) follow.Add(m);
            }
            s.Follow = follow.ToArray();
            s.StepType = StepRx.IsMatch(t.Name);
            shapes[t] = s;
            return s;
        }

        private struct Node { public object O; public int Depth; public string Path; }

        // Normale Reflection statt AccessTools.Field/Property: die schreiben bei jedem Fehlgriff eine Warnung ins Log
        private static readonly Dictionary<Type, List<M>> defMembers = new Dictionary<Type, List<M>>();
        private static List<M> DefMembers(Type t)
        {
            List<M> r;
            if (defMembers.TryGetValue(t, out r)) return r;
            r = new List<M>();
            foreach (string dn in DefNames)
            {
                try
                {
                    FieldInfo f = null;
                    for (Type b = t; b != null && f == null; b = b.BaseType)
                        f = b.GetField(dn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (f != null) { r.Add(new M { Name = dn, Type = f.FieldType, F = f }); continue; }
                    PropertyInfo pi = null;
                    foreach (PropertyInfo x in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        if (x.Name == dn && x.CanRead && x.GetIndexParameters().Length == 0 && x.GetGetMethod() != null) { pi = x; break; }
                    if (pi != null) r.Add(new M { Name = dn, Type = pi.PropertyType, P = pi });
                }
                catch { }
            }
            defMembers[t] = r;
            return r;
        }

        private static List<Req> Extract(QuestData q)
        {
            var reqs = new List<Req>();
            var visited = new HashSet<object>(new RefEq());
            var queue = new Queue<Node>();
            queue.Enqueue(new Node { O = q, Depth = 0, Path = "quest" });
            // Definition der Quest (falls es ein eigenes Objekt dafuer gibt); Member einmal je Typ gesucht
            foreach (M dm in DefMembers(q.GetType()))
            {
                object d = dm.Get(q);
                if (d != null && !Skip(d.GetType())) queue.Enqueue(new Node { O = d, Depth = 1, Path = "quest." + dm.Name });
            }
            int visits = 0;
            bool ambiguous = false;
            while (queue.Count > 0 && visits < MaxVisits && !ambiguous)
            {
                Node nd = queue.Dequeue();
                object o = nd.O;
                if (o == null || !visited.Add(o)) continue;
                visits++;
                if (nd.Depth > 0 && ReadLeaf(o, nd.Path, reqs)) continue;
                if (nd.Depth >= MaxDepth) continue;
                Shape s = ShapeOf(o.GetType());
                int stepIdx = -1;
                if (s.StepIdx != null) { object v = s.StepIdx.Get(o); if (v is int) stepIdx = (int)v; }
                foreach (M m in s.Follow)
                {
                    object v = m.Get(o);
                    if (v == null || v is string) continue;
                    string path = nd.Path + "." + m.Name;
                    if (v is IEnumerable && !(v is GameRes))
                    {
                        if (!ExpandList(o, s, m, (IEnumerable)v, stepIdx, nd.Depth + 1, path, queue)) { ambiguous = true; break; }
                    }
                    else if (!Skip(v.GetType())) queue.Enqueue(new Node { O = v, Depth = nd.Depth + 1, Path = path });
                }
            }
            if (ambiguous)
            {
                LogOnce("ambig:" + q.id, "[QuestItems] quest " + q.id + ": ambiguous steps - no rows shown");
                reqs.Clear();
            }
            return reqs;
        }

        // Sammlung ausklappen, mit Filter auf den aktuellen Schritt; false = mehrdeutige Schrittliste
        private static bool ExpandList(object owner, Shape os, M m, IEnumerable e, int stepIdx, int depth, string path, Queue<Node> queue)
        {
            if (e is IDictionary) e = ((IDictionary)e).Values;
            var items = new List<object>();
            try
            {
                foreach (object x in e)
                {
                    if (x == null) continue;
                    items.Add(x);
                    if (items.Count >= MaxElems) break;
                }
            }
            catch { return true; }
            if (items.Count == 0) return true;
            Type et = items[0].GetType();
            if (Skip(et) || items[0] is QuestData) return true;
            var list = e as IList;
            // Schritt-Index nur auf Listen anwenden, die nach Schritten aussehen (nicht auf requiredItems o. ae. daneben)
            if (stepIdx >= 0 && list != null && stepIdx < list.Count
                && (StepRx.IsMatch(m.Name) || StepRx.IsMatch(et.Name) || ShapeOf(et).StepType))
            {
                object cur = null;
                try { cur = list[stepIdx]; } catch { }
                if (cur != null) queue.Enqueue(new Node { O = cur, Depth = depth, Path = path + "[" + stepIdx + "]" });
                return true;
            }
            Shape es = ShapeOf(et);
            bool filtered = false;
            var keep = new List<object>();
            if (es.DoneFlag != null || es.ActiveFlag != null)
            {
                filtered = true;
                foreach (object x in items)
                {
                    Shape xs = x.GetType() == et ? es : ShapeOf(x.GetType());
                    if (xs.DoneFlag != null && xs.DoneFlag.Get(x) is bool && (bool)xs.DoneFlag.Get(x)) continue;
                    if (xs.ActiveFlag != null && !(xs.ActiveFlag.Get(x) is bool && (bool)xs.ActiveFlag.Get(x))) continue;
                    keep.Add(x);
                }
            }
            else keep = items;
            if (!filtered && items.Count > 1 && (es.StepType || StepRx.IsMatch(m.Name))) return false;
            for (int i = 0; i < keep.Count; i++)
                queue.Enqueue(new Node { O = keep[i], Depth = depth, Path = path + "[" + i + "]" });
            return true;
        }

        // Blatt lesen; true = war ein Blatt (nicht weiter hineinschauen)
        private static bool ReadLeaf(object o, string path, List<Req> reqs)
        {
            // L1: NeedItemData (bekannte API)
            var nid = o as NeedItemData;
            if (nid != null)
            {
                if (string.IsNullOrEmpty(nid.Id)) return true;
                int c;
                try { c = nid.GetCount((WgoData)null); } catch { c = 1; }
                bool g = false;
                try { g = nid.IsGroup; } catch { }
                reqs.Add(new Req { Id = nid.Id, Group = g, Count = c });
                LogLeaf("NeedItemData", "[QuestItems] leaf NeedItemData via " + path);
                return true;
            }
            // L2: GameRes (Schluessel per Reflection, Werte mit GetWithoutSystemsCheck)
            if (o is GameRes) { ReadRes((GameRes)o, path, reqs); return true; }
            if (o is ItemDef || o is CraftDef) return true;   // Definitionen nie als Anforderung lesen
            Shape s = ShapeOf(o.GetType());
            // L3: Objekt mit Gegenstands-ID und Menge
            if ((s.Id != null || s.IdDef != null) && s.Count != null)
            {
                string id = null;
                if (s.IdDef != null) { var d = s.IdDef.Get(o) as ItemDef; if (d != null) id = d.id; }
                if (id == null && s.Id != null) id = s.Id.Get(o) as string;
                bool group;
                if (!string.IsNullOrEmpty(id) && Resolves(id, out group))
                {
                    double cnt = Num(s.Count.Get(o));
                    if (!double.IsNaN(cnt) && cnt > 0)
                    {
                        int count = (int)Math.Round(cnt);
                        string dl = "";
                        if (s.Delivered != null)
                        {
                            double done = Num(s.Delivered.Get(o));
                            if (!double.IsNaN(done) && done >= 0 && done <= cnt) { count = (int)Math.Round(cnt - done); dl = " delivered=" + s.Delivered.Name; }
                        }
                        reqs.Add(new Req { Id = id, Group = group, Count = count });
                        LogLeaf(o.GetType().FullName, "[QuestItems] leaf " + o.GetType().Name + ": id=" + (s.IdDef != null ? s.IdDef.Name : s.Id.Name) + " count=" + s.Count.Name + dl + " via " + path);
                        return true;
                    }
                }
            }
            // L4: Geld auf einem Anforderungs-Pfad
            if (s.Money != null && ReqPathRx.IsMatch(path))
            {
                double mv = Num(s.Money.Get(o));
                if (!double.IsNaN(mv) && mv > 0)
                {
                    reqs.Add(new Req { Id = MoneyId, Count = (int)Math.Round(mv) });
                    LogLeaf(o.GetType().FullName + "#money", "[QuestItems] leaf " + o.GetType().Name + ": money=" + s.Money.Name + " via " + path);
                    return false;   // kann daneben noch Gegenstaende enthalten
                }
            }
            return false;
        }

        private static readonly Dictionary<string, int> resolveCache = new Dictionary<string, int>();   // 0 nein, 1 Gegenstand, 2 Gruppe

        private static bool Resolves(string id, out bool group)
        {
            group = false;
            int r;
            if (!resolveCache.TryGetValue(id, out r))
            {
                r = 0;
                try
                {
                    var n = new NeedItemData(id, 1);
                    List<ItemDef> defs;
                    if (n.IsGroup) { if (n.TryGetGroupItemDefs(out defs) && defs != null && defs.Count > 0) r = 2; }
                    else if (n.ItemDef != null) r = 1;
                }
                catch { r = 0; }
                resolveCache[id] = r;
            }
            group = r == 2;
            return r != 0;
        }

        private static FieldInfo resDict;
        private static bool resDictSearched;
        private static string moneyKey;

        private static FieldInfo ResDict()
        {
            if (!resDictSearched)
            {
                resDictSearched = true;
                for (Type b = typeof(GameRes); b != null && resDict == null; b = b.BaseType)
                    foreach (FieldInfo f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (typeof(IDictionary).IsAssignableFrom(f.FieldType)) { resDict = f; break; }
                Plugin.Log.LogInfo("[QuestItems] GameRes dictionary: " + (resDict != null ? resDict.Name : "not found"));
            }
            return resDict;
        }

        private static void ReadRes(GameRes gr, string path, List<Req> reqs)
        {
            if (ResDict() == null) return;
            var dict = resDict.GetValue(gr) as IDictionary;
            if (dict == null) return;
            var keys = new List<string>();
            foreach (object k in dict.Keys) { var ks = k as string; if (ks != null) keys.Add(ks); }
            foreach (string key in keys)
            {
                if (key.StartsWith("tech_")) continue;
                double v;
                try { v = (double)gr.GetWithoutSystemsCheck(key); } catch { continue; }
                int c = (int)Math.Round(v);
                if (c <= 0) continue;
                bool group;
                if (MoneyKeyRx.IsMatch(key)) { if (moneyKey == null) moneyKey = key; reqs.Add(new Req { Id = MoneyId, Count = c }); }
                else if (Resolves(key, out group)) reqs.Add(new Req { Id = key, Group = group, Count = c });
                else continue;
                LogLeaf("GameRes:" + key, "[QuestItems] leaf GameRes key=" + key + " via " + path);
            }
        }

        // ---------- Geld des Spielers ----------
        private static Func<object, int> moneyGetter;
        private static bool moneySearched;
        private static string loggedMoneyKey;

        internal static int MoneyHave()
        {
            object pd = null;
            try { pd = MainGame.PlayerData; } catch { }
            if (pd == null) return -1;
            if (!moneySearched)
            {
                moneySearched = true;
                FindMoney(pd.GetType());
            }
            if (moneyGetter == null) return -1;
            try { return moneyGetter(pd); } catch { return -1; }
        }

        private static void FindMoney(Type t)
        {
            string found = null;
            try
            {
                // (a) GameRes-Feld/-Property des Spielers mit passendem Namen. Fehlt der Schluessel im GameRes: -1
                // (Zeile weg statt dauerhaft 0/x). Der Schluessel wird erst beim Lesen bestimmt (moneyKey kann spaeter kommen).
                foreach (M m in ShapeOfAll(t))
                {
                    if (m.Type != typeof(GameRes) || !WalletRx.IsMatch(m.Name)) continue;
                    M mm = m;
                    moneyGetter = o =>
                    {
                        var g = mm.Get(o) as GameRes;
                        if (g == null) return -1;
                        string key = moneyKey ?? "money";
                        FieldInfo rd = ResDict();
                        var dict = rd != null ? rd.GetValue(g) as IDictionary : null;
                        if (dict == null || !dict.Contains(key)) return -1;
                        if (key != loggedMoneyKey) { loggedMoneyKey = key; Plugin.Log.LogInfo("[QuestItems] player money key: " + mm.Name + "[" + key + "]"); }
                        return (int)Math.Round((double)g.GetWithoutSystemsCheck(key));
                    };
                    found = "GameRes " + m.Name;
                    break;
                }
                // (b) Zahl namens Money/Coins/Silver bzw. Methode GetMoney()
                if (moneyGetter == null)
                {
                    foreach (string n in new[] { "Money", "money", "Coins", "coins", "Silver", "silver" })
                    {
                        M hit = null;
                        foreach (M m in ShapeOfAll(t)) if (m.Name == n && IsNum(m.Type)) { hit = m; break; }
                        if (hit == null) continue;
                        M h = hit;
                        moneyGetter = o => { double v = Num(h.Get(o)); return double.IsNaN(v) ? -1 : (int)Math.Round(v); };
                        found = h.Name;
                        break;
                    }
                }
                if (moneyGetter == null)
                {
                    MethodInfo mi = null;
                    try { mi = t.GetMethod("GetMoney", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null); } catch { }
                    if (mi != null && IsNum(mi.ReturnType))
                    {
                        moneyGetter = o => { double v = Num(mi.Invoke(o, null)); return double.IsNaN(v) ? -1 : (int)Math.Round(v); };
                        found = "GetMoney()";
                    }
                }
            }
            catch (Exception e) { moneyGetter = null; Plugin.Log.LogWarning("[QuestItems] money lookup failed: " + e.Message); }
            if (moneyGetter == null) Plugin.Log.LogInfo("[QuestItems] player money not found - money rows hidden");
            else Plugin.Log.LogInfo("[QuestItems] player money via " + found);
        }

        // alle Felder + oeffentlichen Properties (ohne Namensfilter), fuer Geld und Dump
        private static readonly Dictionary<Type, M[]> allMembers = new Dictionary<Type, M[]>();
        private static M[] ShapeOfAll(Type t)
        {
            M[] r;
            if (allMembers.TryGetValue(t, out r)) return r;
            var all = new List<M>();
            var seen = new HashSet<string>();
            for (Type b = t; b != null && b != typeof(object); b = b.BaseType)
                foreach (FieldInfo f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    string n = Clean(f.Name);
                    if (seen.Add(n)) all.Add(new M { Name = n, Type = f.FieldType, F = f });
                }
            foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (pi.CanRead && pi.GetIndexParameters().Length == 0 && pi.GetGetMethod() != null && seen.Add(pi.Name))
                    all.Add(new M { Name = pi.Name, Type = pi.PropertyType, P = pi });
            r = all.ToArray();
            allMembers[t] = r;
            return r;
        }

        // ---------- Logging / Diagnose ----------
        private static readonly HashSet<string> logged = new HashSet<string>();
        private static void LogOnce(string key, string msg) { if (logged.Add(key)) Plugin.Log.LogInfo(msg); }
        private static void LogLeaf(string key, string msg) { if (loggedLeaf.Add(key)) Plugin.Log.LogInfo(msg); }

        // einmal je Sitzung: Member von QuestData (Tiefe 1) - aus QuestPinPatch, wenn die Option an ist
        private static bool probed;
        internal static void Probe(QuestData q)
        {
            if (probed || q == null) return;
            probed = true;
            try
            {
                var sb = new StringBuilder("[QuestItems] QuestData members: ");
                int i = 0;
                foreach (M m in ShapeOfAll(q.GetType()))
                {
                    if (i++ >= 60) { sb.Append(" ..."); break; }
                    if (i > 1) sb.Append(", ");
                    sb.Append(m.Name).Append(':').Append(m.Type.Name);
                }
                Plugin.Log.LogInfo(sb.ToString());
            }
            catch (Exception e) { Plugin.Log.LogWarning("[QuestItems] probe: " + e.Message); }
        }

        // Entwickler-Hilfe: liegt BepInEx/gk2_questdump.flag, wird der Member-Baum (Tiefe 3) aller laufenden,
        // sichtbaren Quests einmal ins Log geschrieben - damit lassen sich die Namensmuster nachschaerfen.
        private static bool dumpChecked;
        private static void DumpOnce()
        {
            if (dumpChecked) return;
            dumpChecked = true;
            try
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "gk2_questdump.flag"))) return;
                int n = 0;
                foreach (QuestData q in MainGame.Instance.GameSave.questSystemData.questCollection.quests)
                {
                    if (q == null || q.status != QuestStatus.InProgress || q.isHidden) continue;
                    var sb = new StringBuilder("[QuestItems] dump " + q.id + "\n");
                    Dump(sb, q, 0, new HashSet<object>(new RefEq()));
                    Plugin.Log.LogInfo(sb.ToString());
                    if (++n >= 40) break;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[QuestItems] dump: " + e.Message); }
        }

        private static void Dump(StringBuilder sb, object o, int depth, HashSet<object> seen)
        {
            if (o == null || depth > 3 || sb.Length > 20000 || !seen.Add(o)) return;
            string ind = new string(' ', depth * 2 + 2);
            foreach (M m in ShapeOfAll(o.GetType()))
            {
                if (sb.Length > 20000) { sb.Append(ind).Append("...\n"); return; }
                object v = m.Get(o);
                string vs;
                if (v == null) vs = "null";
                else if (v is string || v.GetType().IsPrimitive || v.GetType().IsEnum) vs = v.ToString();
                else if (v is ICollection) vs = v.GetType().Name + "(" + ((ICollection)v).Count + ")";
                else vs = v.GetType().Name;
                if (vs.Length > 80) vs = vs.Substring(0, 80) + "...";
                sb.Append(ind).Append(m.Name).Append(':').Append(m.Type.Name).Append(" = ").Append(vs).Append('\n');
                if (v == null || v is string) continue;
                if (v is GameRes) continue;
                if (v is IEnumerable)
                {
                    int i = 0;
                    IEnumerable e = v is IDictionary ? ((IDictionary)v).Values : (IEnumerable)v;
                    try
                    {
                        foreach (object x in e)
                        {
                            if (x == null) continue;
                            if (i >= 8) break;
                            sb.Append(ind).Append("  [").Append(i++).Append("] ").Append(x.GetType().Name).Append('\n');
                            if (!Skip(x.GetType())) Dump(sb, x, depth + 2, seen);
                        }
                    }
                    catch { }
                }
                else if (!Skip(v.GetType())) Dump(sb, v, depth + 1, seen);
            }
        }
    }
}
