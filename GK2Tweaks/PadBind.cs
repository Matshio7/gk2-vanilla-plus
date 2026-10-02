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
                if (!info.success) return;
                Set("J:" + info.elementIdentifierId + ":" + (info.elementIdentifierName ?? "?").Replace(":", ""));
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
            return a >= 0 && (down ? p.GetButtonDown(a) : p.GetButton(a));
        }

        internal static bool Held(Player p, string v) => Raw(p, v, false);
        internal static bool Down(Player p, string v) => Raw(p, v, true);
    }
}
