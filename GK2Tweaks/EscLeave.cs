using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Gespraech mit Esc / B (Kreis) verlassen: waehlt die Antwort "Gehen", wenn sie gerade angeboten wird und
    // die Antworten anklickbar sind (NPC hat fertig gesprochen). Gleicher Weg wie ein Mausklick auf die Antwort
    // (Klick-Ton, normales Gespraechsende). Ist ein Mod-Fenster offen, schliesst Esc zuerst das.
    // Idee aus dem Nexus-Mod "ESC to Leave" von OrionAF - eigene Umsetzung.
    internal static class EscLeave
    {
        private const int ActB = 5; // Rewired "Zurueck" (wie in PadNav)
        private static bool otherChecked;
        private static string lastLoggedIds;

        internal static bool OtherMod { get; private set; }

        internal static void Tick(bool modWindowOpen)
        {
            if (!Plugin.EscLeave.Value || !WeekPlan.InGame) return;
            if (!otherChecked)
            {
                otherChecked = true;
                OtherMod = ModCompat.Has("esc", "leave");
                if (OtherMod) Plugin.Log.LogInfo("Esc/B leave: 'ESC to Leave' is installed, own function stays off");
            }
            if (OtherMod || !UIMultiAnswer.IsShowing) return;
            bool pressed = Input.GetKeyDown(KeyCode.Escape);
            if (!pressed)
            {
                try
                {
                    if (Rewired.ReInput.isReady)
                    {
                        Rewired.Player p = Rewired.ReInput.players.GetPlayer(0);
                        pressed = p != null && p.GetButtonDown(ActB);
                    }
                }
                catch { }
            }
            if (!pressed || modWindowOpen) return;

            var list = Traverse.Create(typeof(UIMultiAnswer)).Field("multiAnswers").GetValue<List<UIMultiAnswer>>();
            if (list == null) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                UIMultiAnswer ma = list[i];
                if (ma == null || !ma.isActiveAndEnabled) continue;
                Traverse t = Traverse.Create(ma);
                if (!t.Field("interactable").GetValue<bool>()) return; // Antworten noch nicht waehlbar (NPC spricht, Animation)
                var answers = t.Field("visualData").GetValue<List<AnswerVisualData>>();
                if (answers == null) return;
                string leave = null;
                var ids = new List<string>();
                foreach (AnswerVisualData a in answers)
                {
                    if (a == null || string.IsNullOrEmpty(a.id)) continue;
                    ids.Add(a.id);
                    if (leave == null && IsLeave(a.id) && !(a.answerData != null && a.answerData.notAvailable)) leave = a.id;
                }
                if (leave == null)
                {
                    // nur einmal je Antwort-Satz ins Log (hilft, weitere "Gehen"-Varianten zu finden)
                    string joined = string.Join(",", ids.ToArray());
                    if (joined != lastLoggedIds) { lastLoggedIds = joined; Plugin.Log.LogInfo("Esc/B in conversation: no leave answer among [" + joined + "]"); }
                    return;
                }
                Plugin.Log.LogInfo("Esc/B: leaving conversation (" + leave + ")");
                ma.OnAnswerSelect(leave);
                // Das Spiel soll denselben Tastendruck nicht zusaetzlich auswerten (z. B. Pausenmenue)
                try
                {
                    LazyInput.ClearAllKeysDown();
                    LazyInput.WaitForRelease(GameKey.Back);
                    LazyInput.WaitForRelease(GameKey.InGameMenu); // Esc oeffnet sonst direkt danach das Pausenmenue
                }
                catch { }
                return;
            }
        }

        internal static bool IsLeave(string id) =>
            id == "common_leave" || id == "leave" || id.EndsWith("_leave", StringComparison.Ordinal);
    }
}
