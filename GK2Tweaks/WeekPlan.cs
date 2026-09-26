using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Wochenplan: was an welchem der 6 Wochentage moeglich ist. Spoilerfrei - ein Eintrag erscheint erst,
    // wenn das zugehoerige Tutorial freigeschaltet bzw. die Quest erreicht ist.
    // Morgens eine Spiel-Benachrichtigung "Heute: ...", jederzeit als Fenster per Taste (Standard F6).
    // Idee angeregt durch "Daily Reminder" von MrsKiraSayers; eigene Umsetzung und eigene Texte.
    internal static class WeekPlan
    {
        internal static readonly string[] DayIds = { "day_pride", "day_sloth", "day_gluttony", "day_lust", "day_envy", "day_wrath" };

        private sealed class Entry
        {
            public string Day, De, En;
            public string Tutorial, QuestStarted, QuestDone; // Bedingung (eine davon), leer = immer
        }

        private static readonly Entry[] Entries =
        {
            new Entry { Day = "day_pride", Tutorial = "tut_commerce_hdr", De = "Stadtaufträge annehmen", En = "Town orders can be accepted" },
            new Entry { Day = "day_pride", Tutorial = "tut_trading_hdr", De = "Händler-Zufriedenheit wird zurückgesetzt", En = "Vendor happiness limit resets" },
            new Entry { Day = "day_sloth", QuestDone = "14_guard_faith_bell", De = "Glocke ruft die Zombies ins Stadtzentrum", En = "The bell calls the zombies to the town centre" },
            new Entry { Day = "day_sloth", Tutorial = "tut_battle_1_hdr|tut_battle_2_hdr", De = "Kampftag: Angriffe planen", En = "Battle day: plan your attacks" },
            new Entry { Day = "day_gluttony", Tutorial = "tut_pr_machine_hdr", De = "PR-Maschine befüllen (Zeitungen, Hypno-Geräte)", En = "Fill the PR machine (newspapers, hypno devices)" },
            new Entry { Day = "day_lust", Tutorial = "tut_town_hdr", De = "Grüne Schriftrollen zu Linda bringen", En = "Bring green scrolls to Linda" },
            new Entry { Day = "day_lust", QuestStarted = "36_port_trademaster_lenses", De = "Handelsschiff im Hafen: neue Waren beim Handelsmeister", En = "Merchant ship in port: new goods at the Trade Master" },
            new Entry { Day = "day_lust", QuestStarted = "34_port_looters_meet", De = "Plünderer am Hafen nehmen neue Aufträge an", En = "Looters at the port take new orders" },
            new Entry { Day = "day_envy", Tutorial = "tut_resurrection_hdr", De = "Gewitter: Zombies wiederbeleben", En = "Thunderstorm: resurrect zombies" },
            new Entry { Day = "day_envy", QuestDone = "63_swamp_goddess_speak", De = "Blaue Kristalle bei der Göttinnen-Statue sammeln", En = "Collect blue crystals at the goddess statue" },
            new Entry { Day = "day_wrath", Tutorial = "tutorial_hdr_seremons", De = "Predigt in der Kirche halten", En = "Hold a sermon in the church" },
            new Entry { Day = "day_wrath", QuestStarted = "51_crossroad_fixed_corpse_delivery", De = "Nachfragen, wie viele Leichen diese Woche gebraucht werden", En = "Ask how many corpses are needed this week" },
        };

        private static readonly Dictionary<string, string[]> DayNames = new Dictionary<string, string[]>
        {
            { "day_pride", new[] { "Hochmut", "Pride" } },
            { "day_sloth", new[] { "Trägheit", "Sloth" } },
            { "day_gluttony", new[] { "Völlerei", "Gluttony" } },
            { "day_lust", new[] { "Wollust", "Lust" } },
            { "day_envy", new[] { "Neid", "Envy" } },
            { "day_wrath", new[] { "Zorn", "Wrath" } },
        };

        private static Dictionary<int, string> numberToId;
        private static Dictionary<string, Texture2D> icons;
        private static bool subscribed;

        internal static string DayName(string id) => DayNames.TryGetValue(id, out string[] n) ? n[Labels.German ? 0 : 1] : id;

        internal static bool InGame => MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.InGame && MainGame.Instance.GameSave != null;

        // Wochentag-ID fuer eine Tagesnummer (1..6, wie im Spiel konfiguriert)
        internal static string IdForNumber(int number)
        {
            if (numberToId == null)
            {
                var map = new Dictionary<int, string>();
                foreach (string id in DayIds)
                {
                    try { ConstDef c = ConstDef.Get(id); if (c != null) map[c.IntValue] = id; } catch { }
                }
                if (map.Count < 6) return null;
                numberToId = map;
            }
            return numberToId.TryGetValue(number, out string d) ? d : null;
        }

        internal static int TodayNumber => MainGame.Instance.GameSave.environmentData.CurrentDayNumber;

        private static bool Unlocked(Entry e)
        {
            GameSave save = MainGame.Instance.GameSave;
            try
            {
                if (!string.IsNullOrEmpty(e.Tutorial))
                {
                    List<string> t = save.knowledgeSystem?.unlockedTutorials;
                    if (t == null) return false;
                    foreach (string id in e.Tutorial.Split('|')) if (t.Contains(id)) return true;
                    return false;
                }
                if (!string.IsNullOrEmpty(e.QuestDone))
                    return save.questSystemData.IsQuestInStatus(e.QuestDone, QuestStatus.Completed);
                if (!string.IsNullOrEmpty(e.QuestStarted))
                    return save.questSystemData.IsQuestInStatus(e.QuestStarted, QuestStatus.InProgress)
                        || save.questSystemData.IsQuestInStatus(e.QuestStarted, QuestStatus.Awaiting)
                        || save.questSystemData.IsQuestInStatus(e.QuestStarted, QuestStatus.Completed);
            }
            catch { return false; }
            return true;
        }

        internal static List<string> ItemsFor(string dayId)
        {
            var list = new List<string>();
            if (!InGame) return list;
            foreach (Entry e in Entries)
                if (e.Day == dayId && Unlocked(e)) list.Add(Labels.German ? e.De : e.En);
            return list;
        }

#if DEV
        internal static void DebugDump()
        {
            GameSave save = MainGame.Instance.GameSave;
            Plugin.Log.LogInfo("[WEEK] tutorials: " + string.Join(",", save.knowledgeSystem.unlockedTutorials.ToArray()));
            foreach (Entry e in Entries) Plugin.Log.LogInfo("[WEEK] " + e.Day + " " + (e.Tutorial ?? e.QuestDone ?? e.QuestStarted) + " -> " + Unlocked(e));
        }
#endif

        // ---------- Benachrichtigung am Morgen ----------
        internal static void Init()
        {
            if (subscribed) return;
            subscribed = true;
            EnvironmentEngine.OnNewDayStartedWithDayNumber += OnNewDay;
        }

        internal static void OnNewDay(int dayNumber)
        {
            if (!Plugin.WeekPlanNotify.Value || !InGame) return;
            try
            {
                string id = IdForNumber(dayNumber);
                if (id == null) return;
                List<string> items = ItemsFor(id);
                if (items.Count == 0) return;
                Notify("<sprite name=\"" + id + "\"> " + Labels.T("Heute", "Today") + " (" + DayName(id) + "): " + string.Join(" · ", items.ToArray()));
            }
            catch (Exception e) { Plugin.Log.LogWarning("WeekPlan: " + e.Message); }
        }

        private static void Notify(string text)
        {
            UINotificator n = UINotificator.Instance;
            if (n == null) return;
            UISimpleTextNotification note = LazyPooler.GetObject<UISimpleTextNotification>();
            note.LocalizationKey = "";
            note.Text = text;
            AccessTools.Method(typeof(UINotificator), "ShowNotification").Invoke(n, new object[] { note });
        }

        // ---------- Tages-Symbole aus dem HUD-Rad des Spiels ----------
        internal static Texture2D Icon(string dayId)
        {
            if (icons == null)
            {
                try
                {
                    UIHUDWheel wheel = UnityEngine.Object.FindFirstObjectByType<UIHUDWheel>(FindObjectsInactive.Include);
                    var sprites = wheel != null ? Traverse.Create(wheel).Field("dayIcons").GetValue<List<Sprite>>() : null;
                    if (sprites == null) return null;
                    var d = new Dictionary<string, Texture2D>();
                    foreach (Sprite s in sprites)
                        if (s != null && !d.ContainsKey(s.name)) d[s.name] = GameSkin.Extract(s);
                    icons = d;
                }
                catch { return null; }
            }
            return icons.TryGetValue(dayId, out Texture2D t) ? t : null;
        }
    }
}
