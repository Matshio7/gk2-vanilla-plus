using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Controller fuer Mod-Knoepfe in Fenstern des Spiels: sucht eine Taste, die das Fenster selbst nicht belegt
    // (Y, X, RT oder LT laut Tastenbelegung des Spiels), und fragt sie direkt ueber Rewired ab.
    internal static class PadExtra
    {
        private sealed class Slot { public int Rewired; public string Name; }
        private static readonly Dictionary<Type, Slot> cache = new Dictionary<Type, Slot>();
        private static readonly int[] candGp = { 4, 3, 14, 13 };          // GamepadButton.Y, X, RT, LT (Enumeration-Werte)
        private static readonly int[] candRw = { 3, 2, 9, 8 };            // Rewired-Aktionen dazu
        private static readonly string[] candName = { "Y", "X", "RT", "LT" };

        private static Slot For(object window)
        {
            if (window == null) return null;
            Type t = window.GetType();
            if (cache.TryGetValue(t, out Slot s)) return s;
            var used = new HashSet<int>();
            try
            {
                var keys = new List<GameKey> { GameKey.Select, GameKey.Back };
                // bekannte Tasten der Fenster fest dazu (falls das Auslesen der Fenster-Tasten scheitert)
                if (window is UIVendorWindow) { keys.Add(GameKey.AcceptVendorDeal); keys.Add(GameKey.ItemMove); }
                if (window is UIZombieWorkerWindow) { keys.Add(GameKey.ItemMove); keys.Add(GameKey.NextTab); keys.Add(GameKey.PrevTab); keys.Add(GameKey.NextSubTab); keys.Add(GameKey.PrevSubTab); }
                var d = Traverse.Create(window).Method("GetGameKeyDelegates").GetValue() as System.Collections.IDictionary;
                if (d != null) foreach (object k in d.Keys) if (k is GameKey gk) keys.Add(gk);
                Plugin.Log.LogInfo("Pad: " + window.GetType().Name + " keys=" + keys.Count + " delegates=" + (d != null ? d.Count : -1));
                foreach (GamepadBinding b in LazyInput.GameBindings.gamepadBindings)
                    foreach (GameKey k in keys)
                        if (b != null && b.gameKey != null && b.gamepadButton != null && b.gameKey.value == k.value) used.Add(b.gamepadButton.value);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pad: " + e.Message); }
            // Haendler: Y = "Handeln" (im Test bestaetigt) - nie Y nehmen, auch wenn die Belegung anders aussieht
            if (window is UIVendorWindow) used.Add(4);
            s = null;
            // Haendler: fest LT (Y = Handeln, X = Ware bewegen im Spiel)
            if (window is UIVendorWindow) s = new Slot { Rewired = 8, Name = "LT" };
            else
            for (int i = 0; i < candGp.Length; i++)
                if (!used.Contains(candGp[i])) { s = new Slot { Rewired = candRw[i], Name = candName[i] }; break; }
            cache[t] = s;
            Plugin.Log.LogInfo("Pad: " + t.Name + " used=[" + string.Join(",", new List<int>(used).ConvertAll(x => x.ToString()).ToArray()) + "] -> " + (s != null ? s.Name : "no free button"));
            return s;
        }

        // Tastenname fuer die Anzeige (z. B. "Y"), null = keine freie Taste
        internal static string Name(object window) => For(window)?.Name;

        // frei belegbare Controller-Knoepfe (Einstellungen) -> Rewired-Aktion
        internal static int Id(string name)
        {
            switch (name)
            {
                case "X": return 2; case "Y": return 3; case "A": return 4; case "B": return 5;
                case "LB": return 6; case "RB": return 7; case "LT": return 8; case "RT": return 9;
                default: return -1;
            }
        }

        internal static bool Down(object window)
        {
            Slot s = For(window);
            if (s == null) return false;
            try
            {
                if (!Rewired.ReInput.isReady) return false;
                Rewired.Player p = Rewired.ReInput.players.GetPlayer(0);
                return p != null && p.GetButtonDown(s.Rewired);
            }
            catch { return false; }
        }
    }
}
