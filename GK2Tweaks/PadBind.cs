using System;
using BepInEx.Configuration;
using Rewired;
using UnityEngine;

namespace GK2Tweaks
{
    // Frei belegbare Controller-Taste. Gespeichert als Aktionsname ("RT", "LT" ... wie im Spiel belegt),
    // "Off" oder "J:<Element-ID>:<Name>" fuer eine beliebige physische Taste, die der Spieler gedrueckt hat.
    internal static class PadBind
    {
        internal static ConfigEntry<string> Capturing { get; private set; }
        private static float captureFrom;

        internal static void Start(ConfigEntry<string> e)
        {
            Capturing = e;
            captureFrom = Time.unscaledTime + 0.4f; // den Druck, der das Feld geoeffnet hat, nicht mitnehmen
        }

        internal static void Tick()
        {
            if (Capturing == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Set("Off"); return; }
            if (Time.unscaledTime < captureFrom) return;
            try
            {
                if (!ReInput.isReady) return;
                ControllerPollingInfo info = ReInput.controllers.polling.PollAllControllersOfTypeForFirstButtonDown(ControllerType.Joystick);
                if (info.success)
                {
                    Set("J:" + info.elementIdentifierId + ":" + (info.elementIdentifierName ?? "?").Replace(":", ""));
                    return;
                }
                // Xbox/XInput (Windows): RT/LT sind Achsen, keine Tasten -> der Tasten-Poll oben findet sie nie.
                // Dann ueber die Aktionen des Spiels (RT = 9, LT = 8) erkennen und als Aktionsname speichern.
                Player p = ReInput.players.GetPlayer(0);
                if (p == null) return;
                bool rt = ActDown(p, 9), lt = ActDown(p, 8);   // beide jeden Frame abfragen (Zustand pro Frame)
                // nur ohne gedrueckte Tastatur-/Maustaste: falls das Spiel Aktion 8/9 auch auf eine Taste legt,
                // soll die nicht als "RT"/"LT" gespeichert werden
                if (Input.anyKey) return;
                if (rt) Set("RT");
                else if (lt) Set("LT");
            }
            catch (Exception ex) { Plugin.Log.LogWarning("Pad bind: " + ex.Message); Capturing = null; }
        }

        private static void Set(string v)
        {
            Plugin.Log.LogInfo("Pad bind: " + Capturing.Definition.Key + " = " + v);
            Capturing.Value = v;
            Capturing = null;
        }

        internal static string Display(string v)
        {
            if (string.IsNullOrEmpty(v) || v == "Off") return Labels.T("– aus –", "– off –");
            if (v.StartsWith("J:"))
            {
                string[] p = v.Split(new[] { ':' }, 3);
                return p.Length == 3 ? p[2] : v;
            }
            return v;
        }

        private static bool Raw(Player p, string v, bool down)
        {
            if (p == null || string.IsNullOrEmpty(v) || v == "Off") return false;
            if (v.StartsWith("J:"))
            {
                string[] parts = v.Split(new[] { ':' }, 3);
                if (parts.Length < 2 || !int.TryParse(parts[1], out int id)) return false;
                foreach (Joystick j in p.controllers.Joysticks)
                    if (j != null && (down ? j.GetButtonDownById(id) : j.GetButtonById(id))) return true;
                return false;
            }
            int a = PadExtra.Id(v);
            if (a < 0) return false;
            // Rueckfall fuer Trigger, die nur als Achse kommen (Xbox-Controller unter Windows, RecyclebinHero):
            // bisheriges Verhalten bleibt, die Achse wird nur zusaetzlich gelesen
            return down ? ActDown(p, a) : ActHeld(p, a);
        }

        // Zustand je Rewired-Aktion, einmal pro Frame berechnet (mehrere Abfragen im selben Frame liefern dasselbe)
        private const int MaxAct = 32;
        private static readonly bool[] stHeld = new bool[MaxAct], stDown = new bool[MaxAct], stAxis = new bool[MaxAct], stLatch = new bool[MaxAct];
        private static readonly int[] stFrame = new int[MaxAct];
        private static bool axisLogged, axisErrLogged;

        // Achsenwert der Aktion (0 bei Fehler)
        private static float AxisVal(Player p, int a)
        {
            try { return p.GetAxis(a); }
            catch (Exception ex)
            {
                if (!axisErrLogged) { axisErrLogged = true; Plugin.Log.LogWarning("Pad bind: axis read failed: " + ex.Message); }
                return 0f;
            }
        }

        private static bool Sample(Player p, int a)
        {
            if (p == null || a < 0 || a >= MaxAct) return false;
            int f = Time.frameCount;
            if (stFrame[a] == f) return true;
            bool fresh = stFrame[a] != f - 1;   // im letzten Frame nicht abgefragt (z. B. Feld gerade geoeffnet)
            stFrame[a] = f;
            bool btn = p.GetButton(a), bdown = p.GetButtonDown(a);
            // Schwelle mit Hysterese: an ueber 0.6 (etwas ueber der ueblichen Rewired-Schwelle 0.5 - meldet Rewired die Taste,
            // kommt sie zuerst), aus erst unter 0.4 - ein Trigger, der um 0.6 pendelt, loest nicht mehrfach aus
            float av = AxisVal(p, a);
            bool ax = (stAxis[a] && !fresh) ? av > 0.4f : av > 0.6f;
            bool axEdge = ax && !stAxis[a] && !fresh;   // kein Treffer fuer einen schon gehaltenen Trigger
            stAxis[a] = ax;
            bool active = btn || ax;
            if (fresh) stLatch[a] = active && !bdown;
            // Sperre bis zum Loslassen: Achse und Taste melden denselben Druck nicht zweimal
            stDown[a] = (bdown || axEdge) && !stLatch[a];
            if (stDown[a]) stLatch[a] = true;
            if (!active) stLatch[a] = false;
            stHeld[a] = active;
            if (ax && !btn && !axisLogged)
            {
                axisLogged = true;
                Plugin.Log.LogInfo("Pad bind: action " + a + " seen only as axis value (GetButton false) - axis fallback active");
            }
            return true;
        }

        // Rewired-Aktion gedrueckt (Taste oder Achse > Schwelle), nur im ersten Frame
        internal static bool ActDown(Player p, int a) => Sample(p, a) && stDown[a];
        // Rewired-Aktion gehalten (Taste oder Achse > Schwelle)
        internal static bool ActHeld(Player p, int a) => Sample(p, a) && stHeld[a];

        internal static bool Held(Player p, string v) => Raw(p, v, false);
        internal static bool Down(Player p, string v) => Raw(p, v, true);
    }
}
