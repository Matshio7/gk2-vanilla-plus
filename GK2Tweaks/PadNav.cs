using System;
using BepInEx.Configuration;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Controller-Bedienung der Mod-Fenster (Mod-Menue, Was ist neu?, Wochenplan):
    // Steuerkreuz / linker Stick = Auswahl hoch/runter bzw. Wert aendern, A = ausfuehren/umschalten,
    // B oder Start = schliessen, LB/RB = Eintrag verschieben (Reihenfolge der FPS-Anzeige).
    // Solange ein Mod-Fenster offen ist, bekommt das Spiel keine Controller-Eingaben.
    internal sealed partial class TweaksGui
    {
        // Rewired-Aktionen des Spiels (LazyBearTechnology.GamepadController)
        private const int ActA = 4, ActB = 5, ActLB = 6, ActRB = 7, ActStart = 11, ActUp = 12, ActDown = 13, ActLeft = 14, ActRight = 15;

        private bool padMode;
        private int padFocus, padCount, padCounter, padWin = -1, curWin = -1;
        private bool padUp, padDown, padLeft, padRight, padA, padB, padLB, padRB;
        private Rect padRect;
        private bool padRectInScroll, padRectFresh, inScroll;
        private string padTip;
        private bool inputBlocked, inputWasActive;
        private Vector3 lastMouse;
        private readonly float[] repeatAt = new float[4];
        private readonly bool[] held = new bool[4];
        private static Texture2D focusTex;

        private bool AnyModWindow => menuOpen || newsOpen || weekOpen;

        // aktives Fenster fuer die Controller-Auswahl: das oberste offene
        private int PadWindow => newsOpen ? NewsWindowId : menuOpen ? WindowId : weekOpen ? WeekWindowId : -1;

        // in Update (einmal pro Frame)
        private void PadTick()
        {
            if (!AnyModWindow) { ReleaseInput(); padMode = false; return; }
            BlockInput();
            if (padWin != PadWindow) { padWin = PadWindow; padFocus = 0; }
            if ((Input.mousePosition - lastMouse).sqrMagnitude > 16f || Input.GetMouseButtonDown(0)) padMode = false;
            lastMouse = Input.mousePosition;
            if (capturingKey != null) return;
            try
            {
                if (!Rewired.ReInput.isReady) return;
                Rewired.Player p = Rewired.ReInput.players.GetPlayer(0);
                if (p == null) return;
                float h = p.GetAxis(0), v = p.GetAxis(1);
                bool any = false;
                any |= Dir(0, p.GetButton(ActUp) || v > 0.6f, ref padUp);
                any |= Dir(1, p.GetButton(ActDown) || v < -0.6f, ref padDown);
                any |= Dir(2, p.GetButton(ActLeft) || h < -0.6f, ref padLeft);
                any |= Dir(3, p.GetButton(ActRight) || h > 0.6f, ref padRight);
                if (p.GetButtonDown(ActA)) { padA = true; any = true; }
                if (p.GetButtonDown(ActLB)) { padLB = true; any = true; }
                if (p.GetButtonDown(ActRB)) { padRB = true; any = true; }
                if (p.GetButtonDown(ActB) || p.GetButtonDown(ActStart)) { any = true; PadClose(); }
                if (any && !padMode) { padMode = true; padUp = padDown = padLeft = padRight = padA = padLB = padRB = false; } // erster Druck zeigt nur die Auswahl
            }
            catch { }
        }

        // Richtung mit Wiederholung beim Halten
        private bool Dir(int i, bool now, ref bool flag)
        {
            float t = Time.unscaledTime;
            if (now && !held[i]) { held[i] = true; repeatAt[i] = t + 0.4f; flag = true; return true; }
            if (!now) { held[i] = false; return false; }
            if (t >= repeatAt[i]) { repeatAt[i] = t + 0.11f; flag = true; return true; }
            return false;
        }

        private void PadClose()
        {
            PlaySound("gui_click");
            if (newsOpen) CloseNews();
            else if (menuOpen) SetMenu(false);
            else if (weekOpen) { weekOpen = false; UpdateEnabled(); }
        }

        private void BlockInput()
        {
            if (inputBlocked) return;
            inputBlocked = true;
            try
            {
                inputWasActive = LazyInput.IsInputActive();
                LazyInput.ClearAllKeysDown();
                LazyInput.SetInputActivity(false);
            }
            catch { }
        }

        private void ReleaseInput()
        {
            if (!inputBlocked) return;
            inputBlocked = false;
            try
            {
                LazyInput.SetInputActivity(inputWasActive);
                LazyInput.ClearAllKeysDown();
                LazyInput.WaitForRelease(GameKey.Back);
                LazyInput.WaitForRelease(GameKey.Select);
            }
            catch { }
        }

        // am Anfang von OnGUI: Auswahl hoch/runter (nur im Layout-Durchgang)
        private void PadBeginGUI()
        {
            if (Event.current.type != EventType.Layout) return;
            if (!padMode) { padUp = padDown = padLeft = padRight = padA = padLB = padRB = false; return; }
            int n = Mathf.Max(1, padCount);
            if (padWin == NewsWindowId)
            {
                // "Was ist neu?": hoch/runter scrollt den Text (im Fenster), links/rechts waehlt den Button
                if (padLeft) { padLeft = false; padFocus = (padFocus - 1 + n) % n; PlaySound("gui_hover_light"); }
                if (padRight) { padRight = false; padFocus = (padFocus + 1) % n; PlaySound("gui_hover_light"); }
                return;
            }
            if (padUp) { padUp = false; padFocus = (padFocus - 1 + n) % n; padRectFresh = false; PlaySound("gui_hover_light"); }
            if (padDown) { padDown = false; padFocus = (padFocus + 1) % n; padRectFresh = false; PlaySound("gui_hover_light"); }
        }

        // am Anfang jeder Fensterfunktion
        private void PadWindowBegin(int windowId)
        {
            curWin = windowId;
            padCounter = 0;
            if (windowId == padWin && Event.current.type == EventType.Layout) padTip = null;
        }

        // am Ende jeder Fensterfunktion
        private void PadWindowEnd()
        {
            if (curWin == padWin && Event.current.type == EventType.Layout)
            {
                padCount = padCounter;
                if (padFocus >= padCount) padFocus = Mathf.Max(0, padCount - 1);
                padUp = padDown = padLeft = padRight = padA = padLB = padRB = false; // nicht verbrauchte Tasten verwerfen
            }
            curWin = -1;
        }

        private bool PadFocused()
        {
            if (curWin != padWin || curWin < 0) return false;
            int i = padCounter++;
            return padMode && i == padFocus;
        }

        // Rahmen um das zuletzt gezeichnete Element (Zeile oder Button)
        private void PadMark(bool focused)
        {
            if (!focused || Event.current.type != EventType.Repaint) return;
            Rect r = GUILayoutUtility.GetLastRect();
            padRect = r; padRectInScroll = inScroll; padRectFresh = true;
            if (focusTex == null)
            {
                focusTex = new Texture2D(1, 1);
                focusTex.SetPixel(0, 0, new Color(1f, 0.82f, 0.45f, 1f));
                focusTex.Apply();
            }
            Rect o = new Rect(r.x - 3, r.y - 2, r.width + 6, r.height + 4);
            GUI.DrawTexture(new Rect(o.x, o.y, o.width, 2), focusTex);
            GUI.DrawTexture(new Rect(o.x, o.yMax - 2, o.width, 2), focusTex);
            GUI.DrawTexture(new Rect(o.x, o.y, 2, o.height), focusTex);
            GUI.DrawTexture(new Rect(o.xMax - 2, o.y, 2, o.height), focusTex);
        }

        // eine gemerkte Taste im Layout-Durchgang verbrauchen
        private static bool Take(ref bool flag)
        {
            if (!flag || Event.current.type != EventType.Layout) return false;
            flag = false;
            return true;
        }

        // Scrollbereich so verschieben, dass die Auswahl sichtbar ist (nach EndScrollView aufrufen)
        private void PadScrollInto(ref Vector2 sc, float viewHeight)
        {
            if (!padMode || !padRectFresh || !padRectInScroll || curWin != padWin || Event.current.type != EventType.Repaint) return;
            padRectFresh = false;
            if (padRect.y - 30f < sc.y) sc.y = Mathf.Max(0f, padRect.y - 30f);
            else if (padRect.yMax + 30f > sc.y + viewHeight) sc.y = padRect.yMax + 30f - viewHeight;
        }

        // Button mit Controller-Auswahl
        private bool Btn(string text, GUIStyle style, params GUILayoutOption[] options) => Btn(new GUIContent(text), style, options);

        private bool Btn(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            bool f = PadFocused();
            bool r = GUILayout.Button(content, style, options);
            PadMark(f);
            if (f)
            {
                if (!string.IsNullOrEmpty(content.tooltip)) padTip = content.tooltip;
                if (GUI.enabled && Take(ref padA)) { PlaySound("gui_click"); r = true; }
            }
            return r;
        }

        // Einstellungszeile: links/rechts aendert den Wert, A schaltet um bzw. nimmt den naechsten Wert
        private void PadEntry(bool focused, ConfigEntryBase e, object[] values)
        {
            if (!focused) return;
            padTip = Labels.Tip(e);
            int dir = Take(ref padRight) ? 1 : Take(ref padLeft) ? -1 : Take(ref padA) ? 1 : 0;
            if (dir == 0) return;
            if (e.SettingType == typeof(bool)) e.BoxedValue = !(bool)e.BoxedValue;
            else if (values != null && values.Length > 0)
            {
                int i = Math.Max(0, Array.IndexOf(values, e.BoxedValue));
                e.BoxedValue = values[(i + dir + values.Length) % values.Length];
            }
            else if (e.SettingType == typeof(KeyboardShortcut) && dir != 0) capturingKey = (ConfigEntry<KeyboardShortcut>)e;
            PlaySound("gui_hover_light");
        }

        private static void PlaySound(string id)
        {
            try { LazyAudio.PlayAndForget(id); } catch { }
        }

        // Hinweiszeile unten im Menue
        private string PadFooter()
        {
            if (!padMode) return null;
            string hint = Labels.T("A: auswählen   Links/Rechts: ändern   B: schließen", "A: select   left/right: change   B: close");
            return string.IsNullOrEmpty(padTip) ? hint : padTip + "\n" + hint;
        }
    }
}
