using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2Tweaks
{
    // Mod-Menue in Reitern statt einer langen Liste + "Alles aus" (Vanilla) / "Standard" fuer alle Funktionen.
    internal sealed partial class TweaksGui
    {
        private int menuTab;
        private static readonly string[][] tabNames =
        {
            new[] { "Übersicht", "Overview" },
            new[] { "Grafik & Leistung", "Graphics & speed" },
            new[] { "Spielhilfen", "Gameplay helpers" },
            new[] { "Anpinnen", "Pins" },
            new[] { "Anzeige & Tasten", "Interface & keys" },
        };
        private GUIStyle tabStyle, tabActiveStyle;
        private float masterConfirmUntil;

        internal void TourTab(int t) => SetTab(t);

        private void SetTab(int t)
        {
            int n = tabNames.Length;
            menuTab = ((t % n) + n) % n;
            scroll = Vector2.zero;
            padFocus = 0;
        }

        private void DrawTabs()
        {
            if (tabStyle == null || tabStyle.fontSize != buttonStyle.fontSize - 1)
            {
                tabStyle = new GUIStyle(buttonStyle) { fontSize = buttonStyle.fontSize - 1 };
                tabActiveStyle = new GUIStyle(tabStyle);
                tabActiveStyle.normal = tabStyle.active;
                tabActiveStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
                tabActiveStyle.hover.textColor = tabActiveStyle.normal.textColor;
                tabActiveStyle.fontStyle = FontStyle.Bold;
            }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                int t = i;
                if (Btn(Labels.T(tabNames[i][0], tabNames[i][1]), i == menuTab ? tabActiveStyle : tabStyle)) Defer(() => SetTab(t));
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Labels.T("Controller: LB / RB wechselt den Reiter", "Controller: LB / RB switches tabs"), smallStyle);
        }

        // Alle Funktionen des Mods mit ihrem Vanilla-Wert ("aus"). Tasten, Sprache, Menue-Groesse und der Mods-Knopf
        // bleiben, damit man das Menue weiterhin erreicht.
        private static List<KeyValuePair<ConfigEntryBase, object>> MasterList()
        {
            var l = new List<KeyValuePair<ConfigEntryBase, object>>();
            void Add(ConfigEntryBase e, object off) { if (e != null) l.Add(new KeyValuePair<ConfigEntryBase, object>(e, off)); }
            foreach (var b in new[] { Plugin.HudClock, Plugin.EscLeave, Plugin.WeekPlanNotify, Plugin.ZombieRename, Plugin.TradeLikes, Plugin.CraftMaxButton, Plugin.InstantRemove,
                                      Plugin.FullRefund, Plugin.MoveObjects, Plugin.Respec, Plugin.PauseInBackground, Plugin.FasterTransitions, Plugin.LessMemoryCleanup,
                                      Plugin.MouseWheelZoom, Plugin.SmoothZoom, Plugin.PinsEnabled, Plugin.ShowOverlay, Plugin.HudCenter, Plugin.OledBlack,
                                      Plugin.WideRain, Plugin.MenuExtend, Plugin.MenuModdedLabel, Plugin.SkipIntro })
                Add(b, false);
            foreach (var g in new ConfigEntryBase[] { Plugin.Pacing, Plugin.TargetFps, Plugin.RenderMode, Plugin.Shadows, Plugin.Hbao, Plugin.PointLights,
                                                      Plugin.BackLight, Plugin.Water, Plugin.Clouds, Plugin.PhysicsHz, Plugin.GameLog })
                Add(g, g.DefaultValue);
            Add(Plugin.NoTearing, "Off");
            Add(Plugin.Zoom, 100);
            Add(Plugin.InteriorZoom, 0);
            Add(Plugin.AutoSaveMinutes, 0);
            return l;
        }

        internal static void AllOff()
        {
            foreach (var kv in MasterList())
                try { kv.Key.BoxedValue = kv.Value; } catch (Exception e) { Plugin.Log.LogWarning("All off: " + kv.Key.Definition + " " + e.Message); }
            Plugin.Log.LogInfo("All features off (vanilla)");
        }

        internal static void AllDefault()
        {
            foreach (var kv in MasterList())
                try { kv.Key.BoxedValue = kv.Key.DefaultValue; } catch { }
            Plugin.Log.LogInfo("All features back to default");
        }

        internal static bool IsAllOff()
        {
            foreach (var kv in MasterList()) if (!Equals(kv.Key.BoxedValue, kv.Value)) return false;
            return true;
        }

        // Angepinnte Eintraege mit dem Controller bedienen (die Liste am Bildschirmrand ist nur per Maus klickbar)
        private void DrawPinControls()
        {
            Header(Labels.T("Angepinnt", "Pinned"));
            if (Pins.List.Count == 0)
            {
                GUILayout.Label(Labels.T("Nichts angepinnt. Pinnen geht über die Pinnadel in Rezepten, Bauplänen und Aufgaben.", "Nothing pinned. Use the pin icon in recipes, blueprints and quests."), labelStyle);
                return;
            }
            foreach (Pins.Pin p in Pins.List)
            {
                Pins.Pin pp = p;
                GUILayout.BeginHorizontal();
                GUILayout.Label(p.Title + (p.Ready ? "  (" + Labels.T("bereit", "ready") + ")" : ""), labelStyle, GUILayout.Width(268));
                if (Btn(p.Collapsed ? Labels.T("Aufklappen", "Expand") : Labels.T("Zuklappen", "Collapse"), buttonStyle, GUILayout.Width(120))) Defer(() => PinTree.ToggleCollapsed(pp));
                if (p.QuestId == null && p.Needs.Count > 0)
                {
                    if (Btn("−", arrowStyle, GUILayout.Width(34))) Defer(() => PinTree.StepMult(pp, -1));
                    GUILayout.Label("×" + Math.Max(1, p.Mult), valueStyle, GUILayout.Width(52));
                    if (Btn("+", arrowStyle, GUILayout.Width(34))) Defer(() => PinTree.StepMult(pp, 1));
                }
                if (p.VarCount > 1)
                {
                    if (Btn("<", arrowStyle, GUILayout.Width(34))) Defer(() => PinTree.Switch(pp, -1));
                    if (Btn(">", arrowStyle, GUILayout.Width(34))) Defer(() => PinTree.Switch(pp, 1));
                }
                if (Btn(Labels.T("Lösen", "Unpin"), buttonStyle, GUILayout.Width(90))) Defer(() => Pins.Unpin(pp));
                GUILayout.EndHorizontal();
            }
        }

        private void DrawMaster()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(Labels.T("Alle Funktionen", "All features"), Labels.T(
                "„Alles aus“ schaltet alle Funktionen des Mods ab – das Spiel ist dann wieder Vanilla. Danach einfach nur die 1–2 Dinge einschalten, die du willst. „Standard“ stellt die Empfehlung des Mods wieder her. Tasten, Sprache und der Mods-Knopf bleiben.",
                "\"All off\" turns every feature of the mod off – the game is vanilla again. Then just turn on the 1–2 things you want. \"Default\" restores the mod's recommended setup. Keys, language and the Vanilla+ button stay.")),
                labelStyle, GUILayout.Width(268));
            bool confirm = Time.realtimeSinceStartup < masterConfirmUntil;
            if (Btn(confirm ? Labels.T("Wirklich? Nochmal klicken", "Sure? Click again") : Labels.T("Alles aus (Vanilla)", "All off (vanilla)"), buttonStyle, GUILayout.Width(200)))
            {
                if (confirm) { masterConfirmUntil = 0; Defer(AllOff); ManualSave.Toast(Labels.T("Alles aus – schalte jetzt nur ein, was du willst.", "All off – now turn on only what you want."), 4f); }
                else masterConfirmUntil = Time.realtimeSinceStartup + 4f;
            }
            if (Btn(Labels.T("Standard", "Default"), buttonStyle, GUILayout.Width(106))) { Defer(AllDefault); ManualSave.Toast(Labels.T("Standard-Einstellungen wiederhergestellt.", "Default settings restored."), 3f); }
            GUILayout.EndHorizontal();
        }
    }
}
