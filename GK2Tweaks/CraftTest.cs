#if DEV
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Dev-Test 1.7.2 (ShotSet "crafttest"): haengenden Auftrag nachstellen, Spiel-Verhalten pruefen, Reparatur pruefen,
    // Max-Rechnung und Verschiebe-Sperre pruefen. Speichern ist im Messlauf blockiert. Ausgabe im Log mit [CT].
    internal static class CraftTest
    {
        private static int step;
        private static Wgo wb;
        private static CraftDef def, def2;
        private static CraftInteractionHandler h;
        private static int target;
        private static readonly MethodInfo form = AccessTools.Method(typeof(CraftInteractionHandler), "FormCraftElementAndStartCraft");

        private static void L(string s) => Plugin.Log.LogInfo("[CT] " + s);
        private static void Try(Action a, string what) { try { a(); } catch (Exception e) { Plugin.Log.LogError("[CT] FAIL " + what + ": " + e); } }

        internal static void Tour(float t)
        {
            switch (step)
            {
                case 0: if (t > 3f) { Try(Home, "home"); step++; } break;
                case 10: break;
                case 1: if (t > 12f) { Try(Setup, "setup"); step = 11; } break;
                case 11: if (t > 14f) { if (wb != null) Try(Repro, "repro"); step = 2; } break;
                                case 2: if (t > 17f) { if (wb != null) Try(VanillaBlocked, "vanilla"); step++; } break;
                case 3: if (t > 19f) { if (wb != null) Try(Interact, "interact"); step++; } break;
                case 4: if (t > 22f) { if (wb != null) Try(After, "after"); step++; } break;
                case 5: if (t > 24f) { Try(MaxChecks, "max"); Try(MoveCheck, "move"); step++; } break;
                case 6: if (t > 26f) { L("DONE"); step++; Application.Quit(); } break;
            }
        }

        // Spielstand liegt evtl. drinnen (Kueche ...) - per Karte nach Hause wie im Steam-Bilder-Lauf
        private static void Home()
        {
            AccessTools.Method(typeof(Benchmark), "OpenMap").Invoke(null, null);
            Plugin.Instance.StartCoroutine(HomeLater());
        }
        private static System.Collections.IEnumerator HomeLater()
        {
            yield return new WaitForSecondsRealtime(2f);
            try { AccessTools.Method(typeof(Benchmark), "TeleportHome").Invoke(null, null); } catch (Exception e) { L("teleport " + e.Message); }
        }

        private static UISingleCraftWindowData Data(CraftDef d) => new UISingleCraftWindowData(wb.Data, d, null, null);

        private static string Q()
        {
            CraftComponent cc = wb.Data.CraftComponent;
            var parts = new List<string>();
            foreach (CraftElementBase e in cc.CraftElementsQueue)
                parts.Add(e.CraftId + " x" + e.Count + (e.IsStarted ? " started" : " waiting") + " " + cc.GetStartCraftStatus(e));
            return "status=" + cc.Status + " queue=[" + string.Join(" | ", parts.ToArray()) + "]";
        }

        private static bool Usable(CraftDef d)
        {
            if (d == null || d.isAuto || d.isFuelCraft || d.skipQueue || d.isObjDestroyCraft || d.needItems == null || d.needItems.Count == 0) return false;
            if (d.isNeedsUnlock && !MainGame.Instance.GameSave.knowledgeSystem.unlockedCrafts.Contains(d.id)) return false;
            return true;
        }

        private static void Setup()
        {
            Vector3 pos = MainGame.PlayerController.transform.position;
            var wgos = new List<Wgo>(UnityEngine.Object.FindObjectsByType<Wgo>(FindObjectsSortMode.None));
            wgos.Sort((a, b) => Vector3.Distance(pos, a.transform.position).CompareTo(Vector3.Distance(pos, b.transform.position)));
            foreach (Wgo w in wgos)
            {
                CraftComponent cc = w.Data?.CraftComponent;
                if (cc == null || cc.CraftsIn == null || cc.CraftsIn.Count == 0) continue;
                L("candidate " + w.Data.Definition?.id + " type=" + w.Data.CraftableType + " handler=" + (w.InteractionHandler?.GetType().Name ?? "null") + " queue=" + cc.CraftElementsQueue.Count + " worker=" + (w.Data.Worker != null) + " crafts=" + cc.CraftsIn.Count);
                if (w.Data.CraftableType != CraftableType.Regular) continue;
                if (!(w.InteractionHandler is CraftInteractionHandler ch) || cc.CraftElementsQueue.Count > 0 || w.Data.Worker != null) continue;
                wb = w; def = null; def2 = null;
                foreach (CraftDefBase cb in cc.CraftsIn)
                {
                    CraftDef d = cb as CraftDef;
                    if (!Usable(d)) continue;
                    int tg = CraftMax.Target(Data(d));
                    L("  recipe " + d.id + " max=" + tg);
                    if (tg < 1 || tg > 200) continue;
                    if (def == null) { def = d; target = tg; } else if (def2 == null && d.id != def.id) def2 = d;
                }
                if (def == null) { wb = null; continue; }
                if (def2 == null) def2 = def;
                h = ch;
                L("workbench " + w.Data.Definition.id + " d=" + Vector3.Distance(pos, w.transform.position).ToString("0.0") + " craft " + def.id + " max=" + target + " other " + def2.id + " " + Q());
                return;
            }
            L("FAIL no suitable workbench");
        }

        private static void Start(CraftDef d, int count)
        {
            var data = Data(d);
            h.HasInteraction(MainGame.PlayerController);
            wb.Data.TrySetWorker(MainGame.PlayerController);
            form.Invoke(h, new object[] { d, data.CurrentNeedItems, data.ParamsData, count });
            wb.Data.ClearWorker();
        }

        // so viele Durchgaenge wie moeglich abschliessen (wie der Spieler, der arbeitet)
        private static void Work(int rounds)
        {
            CraftComponent cc = wb.Data.CraftComponent;
            wb.Data.TrySetWorker(MainGame.PlayerController);
            for (int i = 0; i < rounds; i++)
            {
                if (cc.CurrentCraftElement != null && cc.CurrentCraftElement.IsStarted) cc.TryFinishCurCraft();
                else if (!cc.TryContinueFromQueue()) break;
            }
            wb.Data.ClearWorker();
        }

        private static void Repro()
        {
            int count = target + 3;
            Start(def, count);
            L("queued " + def.id + " x" + count + " (ingredients for " + target + ") -> " + Q());
            Work(count * 2 + 5);
            CraftComponent cc = wb.Data.CraftComponent;
            bool stuck = cc.CraftElementsQueue.Exists(e => !e.IsStarted && cc.GetStartCraftStatus(e) != CraftStatus.OK);
            L((stuck ? "REPRO OK - leftover waiting: " : "REPRO no leftover: ") + Q());
        }

        private static void VanillaBlocked()
        {
            CraftComponent cc = wb.Data.CraftComponent;
            int before = cc.CraftElementsQueue.Count;
            bool startedBefore = cc.IsStarted;
            Start(def2, 1);
            L("vanilla F again (" + def2.id + " x1, no repair): queue " + before + " -> " + cc.CraftElementsQueue.Count + " started " + startedBefore + " -> " + cc.IsStarted + " " + Q());
            Work(3);
            L("vanilla after work: " + Q());
        }

        private static void Interact()
        {
            L("before interact: " + Q());
            h.HasInteraction(MainGame.PlayerController);
            bool r = h.Interact(MainGame.PlayerController);
            L("Interact returned " + r + " -> " + Q());
            try { var w1 = LazyUI.GetWindow<UICraftWindow>(); if (w1.IsShown) { L("window open: UICraftWindow"); w1.Close(); } } catch { }
            try { var w2 = LazyUI.GetWindow<UISingleCraftWindow>(); if (w2.IsShown) { L("window open: UISingleCraftWindow"); w2.Close(); } } catch { }
            try { var w3 = LazyUI.GetWindow<UICraftSelectionWindow>(); if (w3.IsShown) { L("window open: UICraftSelectionWindow"); w3.Close(); } } catch { }
            wb.Data.ClearWorker();
        }

        private static void After()
        {
            CraftComponent cc = wb.Data.CraftComponent;
            bool stuck = cc.CraftElementsQueue.Exists(e => !e.IsStarted && cc.GetStartCraftStatus(e) != CraftStatus.OK);
            L((stuck ? "REPAIR FAILED: " : "REPAIR OK: ") + Q());
            int tg = CraftMax.Target(Data(def2));
            if (tg >= 1)
            {
                Start(def2, 1);
                L("new craft after repair (" + def2.id + " x1): " + Q());
                bool ok = cc.IsStarted || cc.CraftElementsQueue.Exists(e => e.CraftId == def2.id);
                Work(3);
                L((ok ? "WORKS AGAIN OK: " : "STILL BLOCKED: ") + Q());
            }
            else L("no ingredients left for " + def2.id + " (max " + tg + "), skip craft-again check");
        }

        private static void MaxChecks()
        {
            if (wb == null) return;
            foreach (CraftDefBase cb in wb.Data.CraftComponent.CraftsIn)
            {
                CraftDef d = cb as CraftDef;
                if (!Usable(d)) continue;
                var data = Data(d);
                MultiInventory inv = wb.Data.GetCraftableMultiInventory();
                var need = new List<string>();
                foreach (NeedItemData n in data.CurrentNeedItems) need.Add(n.Id + " " + inv.GetTotalCount(n.Id) + "/" + n.GetCount(wb.Data));
                L("max " + d.id + " = " + CraftMax.Target(data) + (d.hasDurabilityUseItem ? " tool " + d.durabilityUseItem.Id + " use " + d.needItemsDurabilityUse : "") + " needs [" + string.Join(", ", need.ToArray()) + "]");
            }
        }

        private static void MoveCheck()
        {
            MethodInfo why = AccessTools.Method(typeof(BuildMove), "Why");
            int f = 0, n = 0;
            foreach (Wgo w in UnityEngine.Object.FindObjectsByType<Wgo>(FindObjectsSortMode.None))
            {
                if (w?.Data == null) continue;
                bool factory = w.Data.CraftableType == CraftableType.ConveyorWorkbench;
                if (factory ? f >= 2 : n >= 2) continue;
                if (!factory && (w.Data.CraftComponent == null || w.Data.CraftableType != CraftableType.Regular)) continue;
                object[] args = { w, null };
                string r = why.Invoke(null, args) as string;
                L("move " + (factory ? "factory " : "normal ") + w.Data.Definition.id + " -> " + (r ?? "allowed"));
                if (factory) f++; else n++;
            }
            if (f == 0) L("move: no factory machine in this scene");
        }
    }
}
#endif
