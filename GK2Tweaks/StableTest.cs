#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Dev-Test 1.7.3 stable (ShotSet "stabletest"): Warteschlange bleibt (Reparatur aus), Reparatur nur vorderster Auftrag,
    // Kampf-Backups beim Aufraeumen, Nadel-Groesse (Bild), Mod-Menue-Eintraege (Bild). Ausgabe mit [STB].
    internal static class StableTest
    {
        private static int step;
        private static Wgo wb;
        private static CraftInteractionHandler h;
        private static CraftDef def, def2;
        private static int target;
        private static readonly MethodInfo form = AccessTools.Method(typeof(CraftInteractionHandler), "FormCraftElementAndStartCraft");

        private static void L(string s) => Plugin.Log.LogInfo("[STB] " + s);
        private static void Try(Action a, string what) { try { a(); } catch (Exception e) { Plugin.Log.LogError("[STB] FAIL " + what + ": " + e); } }
        // Anpinnen: Linksklick bei Mouse0 oder leer, Tasten-Modus bei anderen Tasten/Maustasten
        private static void PinKeyLogic()
        {
            var e = Plugin.PinsKey; var old = e.Value;
            e.Value = new BepInEx.Configuration.KeyboardShortcut(KeyCode.Mouse0); bool a = PinButton.KeyMode;
            e.Value = BepInEx.Configuration.KeyboardShortcut.Empty; bool b = PinButton.KeyMode;
            e.Value = new BepInEx.Configuration.KeyboardShortcut(KeyCode.Mouse2); bool c = PinButton.KeyMode;
            e.Value = new BepInEx.Configuration.KeyboardShortcut(KeyCode.G); bool d = PinButton.KeyMode;
            e.Value = old;
            var locked = new System.Collections.Generic.List<string>();
            foreach (TalentLevelUpDef t in GameBalance.Me.talentLevelUpDefs)
                if (Respec.QuestLocked(t)) locked.Add(t.id + (t.isHidden ? "[hidden]" : "") + (t.expressionsOnBuy != null && t.expressionsOnBuy.Count > 0 ? "[expr]" : "") + (t.isZombiePerk ? "[zombie]" : ""));
            L("QUEST TALENTS " + locked.Count + ": " + string.Join(", ", locked.ToArray()));
            L("PIN KEY " + (!a && !b && c && d ? "OK" : "FAIL") + " mouse0=" + a + " empty=" + b + " mouse2=" + c + " G=" + d + " default=" + e.DefaultValue + " names=" + TweaksGui.PinKeyName(KeyCode.Mouse0) + "|" + TweaksGui.PinKeyName(KeyCode.Mouse3));
        }

        private static void Shot(string name) { string p = Path.Combine(BepInEx.Paths.BepInExRootPath, "shot_" + name + ".png"); ScreenCapture.CaptureScreenshot(p); L("screenshot " + p); }

        internal static void Tour(float t)
        {
            switch (step)
            {
                case 0: if (t > 3f) { Try(Home, "home"); step++; } break;
                case 1: if (t > 13f) { Try(Setup, "setup"); step++; } break;
                case 2: if (t > 15f) { if (wb != null) Try(QueueKept, "queue kept"); step++; } break;
                case 3: if (t > 18f) { if (wb != null) Try(RepairFront, "repair front"); step++; } break;
                case 4: if (t > 21f) { Try(Battle, "battle"); Try(OpenBench, "open bench"); step++; } break;
                case 5: if (t > 24f) { Shot("stb_pins"); step++; } break;
                case 6: if (t > 25f) { Try(CloseCraft, "close"); Try(MenuTab, "menu"); step++; } break;
                case 7: if (t > 28f) { Shot("stb_menu"); step++; } break;
                case 8: if (t > 29f) { Try(() => { Plugin.Instance.Gui.SetMenu(false); Plugin.Instance.Gui.ShowNews(true); }, "news"); step++; } break;
                case 9: if (t > 32f) { Shot("stb_news"); step++; } break;
                case 10: if (t > 33f) { Try(() => Plugin.Instance.Gui.NewsScrollTo(140f), "news scroll"); step++; } break;
                case 11: if (t > 35f) { Shot("stb_news_scrolled"); step++; } break;
                case 12: if (t > 36f) { Try(PinKeyLogic, "pin key"); Try(() => { Plugin.Instance.Gui.ShowNews(false); Plugin.Instance.Gui.ShowCelebration(null); }, "celebration"); step++; } break;
                case 13: if (t > 39f) { Shot("stb_celebration"); step++; } break;
                // 1.7.4
                case 14: if (t > 40f) { Try(() => AccessTools.Method(typeof(TweaksGui), "CloseCeleb").Invoke(Plugin.Instance.Gui, null), "close celebration"); Try(MoveSpeedFormula, "move speed"); Try(PinsFull, "pins full"); step++; } break;
                case 15: if (t > 41f) { Shot("stb_pins_full"); Try(HudFallbackStart, "hud fallback"); step++; } break;
                case 16: if (t > 43f) { Try(HudFallbackCheck, "hud fallback check"); Try(TownPinStart, "town pin"); step++; } break;
                case 17: if (t > 49f) { Try(TownPinCheck, "town pin check"); step++; } break;
                case 18: if (t > 50f) { Try(StuckRunning, "stuck running"); step++; } break;
                case 19: if (t > 51f) { Shot("stb_repair_toast"); Plugin.Instance.StartCoroutine(WalkTest()); step++; } break;
                case 20: if (t > 58f) { L("DONE"); step++; Application.Quit(); } break;
            }
        }

        // ---------- 1.7.4 ----------
        private static void MoveSpeedFormula()
        {
            PlayerPhysicalBody body = MainGame.PlayerController.PhysicalBody;
            int old = Plugin.MoveSpeed.Value;
            float b = body.SpeedMultiplier;
            Plugin.MoveSpeed.Value = 150; float a = MoveSpeed.Scaled(body);
            Plugin.MoveSpeed.Value = 100; float c = MoveSpeed.Scaled(body);
            Plugin.MoveSpeed.Value = old;
            bool ok = MoveSpeed.Hooked == 1 && Mathf.Abs(a - b * 1.5f) < 0.001f && Mathf.Abs(c - b) < 0.001f;
            L((ok ? "MOVE SPEED OK" : "MOVE SPEED FAIL") + " hooked=" + MoveSpeed.Hooked + " base=" + b + " at150=" + a + " at100=" + c);
        }

        // echtes Laufen: 1 s nach rechts mit 100 %, 1 s nach links mit 150 % - Strecke vergleichen
        private static System.Collections.IEnumerator WalkTest()
        {
            PlayerPhysicalBody body = MainGame.PlayerController.PhysicalBody;
            int old = Plugin.MoveSpeed.Value;
            float[] dist = new float[2];
            for (int run = 0; run < 2; run++)
            {
                Plugin.MoveSpeed.Value = run == 0 ? 100 : 150;
                Vector2 dir = run == 0 ? Vector2.right : Vector2.left;
                Vector3 p0 = body.transform.position;
                float until = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < until) { body.MoveByDirection(dir); yield return new WaitForFixedUpdate(); }
                for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
                dist[run] = Vector3.Distance(p0, body.transform.position);
            }
            Plugin.MoveSpeed.Value = old;
            float ratio = dist[0] > 0.01f ? dist[1] / dist[0] : 0f;
            L((ratio > 1.3f && ratio < 1.7f ? "WALK OK" : "WALK CHECK") + " 100%=" + dist[0].ToString("0.00") + " 150%=" + dist[1].ToString("0.00") + " ratio=" + ratio.ToString("0.00"));
        }

        private static List<Pins.Pin> savedPins;
        private static string townId;

        private static void PinsFull()
        {
            savedPins = new List<Pins.Pin>(Pins.List);
            Pins.List.Clear();
            for (int i = 0; i <= Pins.Max; i++) Pins.Toggle(new Pins.Pin { Key = "test:" + i, Title = "Test " + i });
            bool ok = Pins.List.Count == Pins.Max && Pins.List[0].Key == "test:1" && Pins.List[Pins.Max - 1].Key == "test:" + Pins.Max;
            L((ok ? "PINS FULL OK" : "PINS FULL FAIL") + " count=" + Pins.List.Count + " first=" + (Pins.List.Count > 0 ? Pins.List[0].Key : "-") + " (saved " + savedPins.Count + " real pins)");
        }

        private static void TownPinStart()
        {
            Pins.List.Clear();
            HashSet<string> built = Pins.BuiltTownBuildings();
            L("TOWN BUILT " + built.Count + ": " + string.Join(", ", built.Take(15).ToArray()));
            townId = built.FirstOrDefault();
            if (townId == null) { L("TOWN PIN SKIP (nothing built in this save)"); return; }
            Pins.Toggle(new Pins.Pin { Key = "town:" + townId, Title = Pins.Loc(townId) });
        }

        private static void TownPinCheck()
        {
            if (townId != null) L((!Pins.IsPinned("town:" + townId) ? "TOWN PIN OK " : "TOWN PIN FAIL ") + townId);
            Pins.List.Clear();
            if (savedPins != null) Pins.List.AddRange(savedPins);
            PinButton.RefreshAll();
            PinStore.Save();
            L("pins restored: " + Pins.List.Count);
        }

        private static void HudFallbackStart() { HudClock.TestReset(); HudClock.TestThrow = 1; }

        private static void HudFallbackCheck()
        {
            WorldZoneWidget w = GUIElements.Instance != null ? GUIElements.Instance.WorldZoneWidget : null;
            TMPro.TMP_Text t = w != null ? Traverse.Create(w).Field("worldZoneLabel").GetValue<TMPro.TMP_Text>() : null;
            string txt = t != null ? t.text ?? "" : "";
            int i = txt.IndexOf("\n" + HudClock.Mark, StringComparison.Ordinal);
            string line = i >= 0 ? txt.Substring(i + 1 + HudClock.Mark.Length) : "";
            bool ok = HudClock.Level == 1 && System.Text.RegularExpressions.Regex.IsMatch(line, "^[0-9][0-9]:[0-9][0-9]</color>");
            L((ok ? "HUD FALLBACK OK" : "HUD FALLBACK FAIL") + " level=" + HudClock.Level + " line=" + line);
            HudClock.TestReset();
        }

        // Zustand wie bei piermaz38: Werkbank steht auf "laeuft", der Auftrag ist aber nicht gestartet -> F loest es
        private static void StuckRunning()
        {
            if (wb == null || def == null) { L("STUCK RUNNING SKIP (no workbench)"); return; }
            CraftComponent cc = wb.Data.CraftComponent;
            foreach (var e in cc.CraftElementsQueue.ToList()) cc.RemoveFromQueue(e, true);
            var data = Data(def);
            cc.AddToQueue(new CraftElement(def.id, 1, data.CurrentNeedItems, data.ParamsData));
            cc.Status = CraftComponentStatus.Started;
            L("stuck before F: status=" + cc.Status + " " + Q());
            Plugin.RepairWorkbenches.Value = true;
            Interact();
            CraftElementBase cur = cc.CurrentCraftElement;
            bool ok = cc.Status == CraftComponentStatus.QueueDelayed || (cc.Status == CraftComponentStatus.Started && cur != null && cur.IsStarted);
            L((ok ? "STUCK RUNNING OK" : "STUCK RUNNING FAIL") + " -> status=" + cc.Status + " " + Q());
            Plugin.RepairWorkbenches.Value = false;
            foreach (var e in cc.CraftElementsQueue.ToList()) cc.RemoveFromQueue(e, true);
        }

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
            return "[" + string.Join(" | ", cc.CraftElementsQueue.Select(e => e.CraftId + " x" + e.Count + (e.IsStarted ? " started" : " waiting") + " " + cc.GetStartCraftStatus(e)).ToArray()) + "]";
        }

        private static void Setup()
        {
            Vector3 pos = MainGame.PlayerController.transform.position;
            var wgos = new List<Wgo>(UnityEngine.Object.FindObjectsByType<Wgo>(FindObjectsSortMode.None));
            wgos.Sort((a, b) => Vector3.Distance(pos, a.transform.position).CompareTo(Vector3.Distance(pos, b.transform.position)));
            foreach (Wgo w in wgos)
            {
                CraftComponent cc = w.Data?.CraftComponent;
                if (cc == null || cc.CraftsIn == null || w.Data.CraftableType != CraftableType.Regular) continue;
                if (!(w.InteractionHandler is CraftInteractionHandler ch) || cc.CraftElementsQueue.Count > 0 || w.Data.Worker != null) continue;
                wb = w; def = null; def2 = null;
                foreach (CraftDefBase cb in cc.CraftsIn)
                {
                    CraftDef d = cb as CraftDef;
                    if (d == null || d.isAuto || d.isFuelCraft || d.skipQueue || d.isObjDestroyCraft || d.needItems == null || d.needItems.Count == 0) continue;
                    if (d.isNeedsUnlock && !MainGame.Instance.GameSave.knowledgeSystem.unlockedCrafts.Contains(d.id)) continue;
                    int tg = CraftMax.Target(Data(d));
                    if (def == null && tg >= 1 && tg <= 200) { def = d; target = tg; }
                    else if (def2 == null && def != null && d.id != def.id) def2 = d;
                }
                if (def == null || def2 == null) { wb = null; continue; }
                h = ch;
                L("workbench " + w.Data.Definition.id + " craft " + def.id + " max=" + target + " other " + def2.id + " repair=" + Plugin.RepairWorkbenches.Value);
                return;
            }
            L("FAIL no workbench");
        }

        private static void Start(CraftDef d, int count)
        {
            var data = Data(d);
            h.HasInteraction(MainGame.PlayerController);
            wb.Data.TrySetWorker(MainGame.PlayerController);
            form.Invoke(h, new object[] { d, data.CurrentNeedItems, data.ParamsData, count });
            wb.Data.ClearWorker();
        }

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

        private static void Interact()
        {
            h.HasInteraction(MainGame.PlayerController);
            h.Interact(MainGame.PlayerController);
            try { var w1 = LazyUI.GetWindow<UICraftWindow>(); if (w1.IsShown) w1.Close(); } catch { }
            try { var w2 = LazyUI.GetWindow<UISingleCraftWindow>(); if (w2.IsShown) w2.Close(); } catch { }
            wb.Data.ClearWorker();
        }

        // Reparatur AUS (Standard): Auftrag mit fehlenden Zutaten + zweiter wartender Auftrag bleiben beide, auch nach F
        private static void QueueKept()
        {
            Plugin.RepairWorkbenches.Value = false;
            Start(def, target + 3);
            Work((target + 3) * 2 + 5);
            var data = Data(def2);
            var ce = new CraftElement(def2.id, 50, data.CurrentNeedItems, data.ParamsData);
            wb.Data.CraftComponent.AddToQueue(ce);
            L("queue before F: " + Q());
            int before = wb.Data.CraftComponent.CraftElementsQueue.Count;
            Interact();
            int after = wb.Data.CraftComponent.CraftElementsQueue.Count;
            L((before >= 2 && after == before ? "QUEUE KEPT OK" : "QUEUE KEPT FAIL") + " (" + before + " -> " + after + ") " + Q());
        }

        // Reparatur AN: nur der vorderste wartende Auftrag geht raus, der Rest bleibt
        private static void RepairFront()
        {
            Plugin.RepairWorkbenches.Value = true;
            int before = wb.Data.CraftComponent.CraftElementsQueue.Count;
            string front = wb.Data.CraftComponent.CraftElementsQueue[0].CraftId;
            Interact();
            var q = wb.Data.CraftComponent.CraftElementsQueue;
            bool ok = q.Count == before - 1 && (q.Count == 0 || q[0].CraftId != front || before > 2);
            L((ok ? "REPAIR FRONT OK" : "REPAIR FRONT FAIL") + " (" + before + " -> " + q.Count + ", removed " + front + ") " + Q());
            // aufraeumen: Rest raus, Einstellung zurueck
            foreach (var e in q.ToList()) wb.Data.CraftComponent.RemoveFromQueue(e);
            Plugin.RepairWorkbenches.Value = false;
        }

        private static void Battle()
        {
            string slot = "ZZ_Test";
            string dir = Path.Combine(Backups.Root, slot);
            if (Directory.Exists(dir)) Backups.DeleteDir(dir);
            for (int i = 0; i < 8; i++)
            {
                string d = Path.Combine(dir, new DateTime(2026, 10, 8, 20, i, 0).ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, slot + ".dat"), "x");
                if (i > 0) File.WriteAllText(Path.Combine(d, "battle.txt"), "b");
            }
            Directory.CreateDirectory(Path.Combine(dir, "2026-10-01_10-00-00")); // leerer Rest (Wine)
            AccessTools.Method(typeof(Backups), "Prune").Invoke(null, new object[] { slot, 3 });
            var left = Directory.GetDirectories(dir).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            bool ok = left.SequenceEqual(new[] { "2026-10-08_20-00-00", "2026-10-08_20-05-00", "2026-10-08_20-06-00", "2026-10-08_20-07-00" });
            L((ok ? "BATTLE PRUNE OK: " : "BATTLE PRUNE FAIL: ") + string.Join(", ", left) + "  in battle now: " + Backups.InBattle());
            Backups.DeleteDir(dir);
            L("test dir removed: " + !Directory.Exists(dir));
        }

        private static void OpenBench()
        {
            if (wb == null) return;
            LazyUI.GetWindow<UICraftWindow>().Open(new UIBaseCraftWindowData(wb, ce => { }, ce => { }), null);
        }

        private static void CloseCraft()
        {
            try { var w1 = LazyUI.GetWindow<UICraftWindow>(); if (w1.IsShown) w1.Close(); } catch { }
        }

        private static void MenuTab()
        {
            var gui = Plugin.Instance.Gui;
            gui.SetMenu(true);
            AccessTools.Method(typeof(TweaksGui), "SetTab").Invoke(gui, new object[] { 4 });
            Traverse.Create(gui).Field("scroll").SetValue(new Vector2(0, 1400));
        }
    }
}
#endif
