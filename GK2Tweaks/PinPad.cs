using System;
using UnityEngine;

namespace GK2Tweaks
{
    // Pin-Liste mit dem Controller: RT gedrueckt halten -> die Liste bekommt eine Auswahl, das Spiel bekommt solange
    // keine Eingaben. Hoch/runter = Eintrag, A = auf-/zuklappen, links/rechts = Menge, LB/RB = Rezept-Variante,
    // Y = loslösen. RT loslassen = zurueck ins Spiel.
    internal sealed partial class TweaksGui
    {
        private const int ActX = 2, ActY = 3, ActRT = 9;
        private bool pinPad;                 // RT wird gehalten und die Liste ist sichtbar
        private int pinFocus, pinFocusCount;
        private int pinCmdMove, pinCmdLR;    // ausstehende Befehle aus Update (in OnGUI ausgefuehrt)
        private bool pinCmdA, pinCmdY, pinCmdLB, pinCmdRB;
        private readonly float[] ppRepeat = new float[4];
        private readonly bool[] ppHeld = new bool[4];

        private bool PinsVisible => Pins.List.Count > 0 && Plugin.PinsEnabled.Value && WeekPlan.InGame && !HudToggle.Hidden && !BigGameWindowOpen();

        private bool PpDir(int i, bool now)
        {
            float t = Time.unscaledTime;
            if (now && !ppHeld[i]) { ppHeld[i] = true; ppRepeat[i] = t + 0.4f; return true; }
            if (!now) { ppHeld[i] = false; return false; }
            if (t >= ppRepeat[i]) { ppRepeat[i] = t + 0.12f; return true; }
            return false;
        }

        // in Tick (Update), vor PadTick
        private void PinPadTick()
        {
            bool want = false;
            Rewired.Player p = null;
            try
            {
                if (Rewired.ReInput.isReady) p = Rewired.ReInput.players.GetPlayer(0);
                want = p != null && p.GetButton(ActRT) && PinsVisible && !menuOpen && !newsOpen && !weekOpen && !celebOpen && !Renaming;
            }
            catch { want = false; }
            if (want && !pinPad) { pinPad = true; PlaySound("gui_hover_light"); for (int i = 0; i < 4; i++) ppHeld[i] = true; }
            if (!want && pinPad) { pinPad = false; }
            if (!pinPad || p == null) return;
            try
            {
                float h = p.GetAxis(0), v = p.GetAxis(1);
                if (PpDir(0, p.GetButton(ActUp) || v > 0.6f)) pinCmdMove--;
                if (PpDir(1, p.GetButton(ActDown) || v < -0.6f)) pinCmdMove++;
                if (PpDir(2, p.GetButton(ActLeft) || h < -0.6f)) pinCmdLR--;
                if (PpDir(3, p.GetButton(ActRight) || h > 0.6f)) pinCmdLR++;
                if (p.GetButtonDown(ActA)) pinCmdA = true;
                if (p.GetButtonDown(ActY)) pinCmdY = true;
                if (p.GetButtonDown(ActLB)) pinCmdLB = true;
                if (p.GetButtonDown(ActRB)) pinCmdRB = true;
            }
            catch { }
        }

        // Fokus-Ziel in der Liste: Kopfzeile eines Pins (row == null) oder aufklappbare Zutat
        private int pinIdx;

        // Wird je fokussierbarem Element in DrawPins aufgerufen; true = dieses Element hat den Fokus
        private bool PinFocusHere(Rect r)
        {
            int me = pinIdx++;
            if (!pinPad || me != pinFocus) return false;
            if (Event.current.type == EventType.Repaint)
            {
                Color gold = new Color(1f, 0.82f, 0.35f, 1f);
                KitFill(new Rect(r.x - 2f, r.y - 1f, r.width + 4f, 2f), gold);
                KitFill(new Rect(r.x - 2f, r.yMax - 1f, r.width + 4f, 2f), gold);
                KitFill(new Rect(r.x - 2f, r.y - 1f, 2f, r.height + 2f), gold);
                KitFill(new Rect(r.xMax, r.y - 1f, 2f, r.height + 2f), gold);
            }
            return true;
        }

        private void PinPadBegin() { pinIdx = 0; }

        // nach DrawPins: Befehle anwenden (nur einmal, im Repaint-Durchgang)
        private void PinPadEnd(Rect listRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            pinFocusCount = pinIdx;
            if (!pinPad) { pinCmdMove = pinCmdLR = 0; pinCmdA = pinCmdY = pinCmdLB = pinCmdRB = false; return; }
            if (pinFocusCount > 0 && pinCmdMove != 0)
            {
                pinFocus = ((pinFocus + pinCmdMove) % pinFocusCount + pinFocusCount) % pinFocusCount;
                PlaySound("gui_hover_light");
            }
            if (pinFocus >= pinFocusCount) pinFocus = Mathf.Max(0, pinFocusCount - 1);
            pinCmdMove = 0;
            pinCmdLR = 0; pinCmdA = pinCmdY = pinCmdLB = pinCmdRB = false;
            // Hinweis unter der Liste
            EnsureKit();
            string hint = Labels.T("A auf/zu · ←→ Menge · LB/RB Variante · Y lösen · RT loslassen = fertig", "A expand · ←→ amount · LB/RB variant · Y unpin · release RT = done");
            int ofs = kitText.fontSize; kitText.fontSize = 14;
            Vector2 sz = kitText.CalcSize(new GUIContent(hint));
            var hr = new Rect(listRect.xMax - sz.x - 20f, listRect.yMax + 4f, sz.x + 20f, 26f);
            KitFrame(hr, KitSlate);
            GUI.Label(hr, hint, kitText);
            kitText.fontSize = ofs;
        }

        // Befehle fuer das fokussierte Element abholen (einmal verbrauchen)
        private bool TakeCmd(ref bool flag) { if (!flag || Event.current.type != EventType.Repaint) return false; flag = false; return true; }
        private int TakeLR() { if (Event.current.type != EventType.Repaint) return 0; int v = pinCmdLR; pinCmdLR = 0; return v; }
    }
}
