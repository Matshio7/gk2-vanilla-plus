using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2Tweaks
{
    // Meilenstein (zuletzt 3.500+ Spieler): einmalig ein Danke-Banner mit Pixel-Konfetti im Hauptmenue, danach nur noch ein kleiner
    // Hinweis neben "modded" (siehe ModdedLabelPatch).
    internal sealed partial class TweaksGui
    {
        private const int CelebWindowId = 0x6B31;
        // Neuer Meilenstein: die Texte hier und in MainMenu.cs aendern (als feste Texte, damit tools/extract_strings.py
        // sie fuer die Uebersetzungen findet) UND den Config-Schluessel "Celebrated…" in Plugin.cs hochzaehlen -> jeder sieht ihn einmal
        private bool celebOpen;
        private Rect celebWin;
        private Action afterCeleb;
        private GUIStyle celebBigStyle, celebTextStyle;

        private struct Bit { public float x, y, vx, vy, size, phase; public Color c; }
        private readonly List<Bit> bits = new List<Bit>();
        private float confettiStart, lastConfetti;
        private int bursts;
        private static Texture2D whiteTex;
        private static readonly Color[] confettiColors =
        {
            new Color(0.91f, 0.77f, 0.42f), new Color(0.56f, 0.82f, 0.56f), new Color(0.85f, 0.36f, 0.32f),
            new Color(0.96f, 0.92f, 0.80f), new Color(0.62f, 0.48f, 0.82f), new Color(0.38f, 0.74f, 0.78f),
        };

        internal bool CelebOpen => celebOpen;

        internal void ShowCelebration(Action after)
        {
            celebOpen = true;
            afterCeleb = after;
            celebWin = new Rect(0, 0, 0, 0);
            bits.Clear();
            bursts = 0;
            confettiStart = lastConfetti = Time.realtimeSinceStartup;
            Plugin.Celebrated.Value = true;
            try { LazyBearTechnology.LazyAudio.PlayAndForget("unlock"); } catch { }
            UpdateEnabled();
        }

        private void CloseCeleb()
        {
            celebOpen = false;
            bits.Clear();
            UpdateEnabled();
            Action a = afterCeleb;
            afterCeleb = null;
            a?.Invoke();
        }

        private void Burst(float w, int n)
        {
            var r = new System.Random(bursts * 7919 + 13);
            for (int i = 0; i < n; i++)
            {
                bits.Add(new Bit
                {
                    x = (float)r.NextDouble() * w,
                    y = -20f - (float)r.NextDouble() * 260f,
                    vx = ((float)r.NextDouble() - 0.5f) * 60f,
                    vy = 40f + (float)r.NextDouble() * 90f,
                    size = 4f + r.Next(0, 3) * 2f,
                    phase = (float)r.NextDouble() * 6.28f,
                    c = confettiColors[r.Next(confettiColors.Length)],
                });
            }
            bursts++;
        }

        // Konfetti (hinter dem Banner-Fenster, ueber dem Hauptmenue des Spiels)
        private void DrawConfetti(float scale)
        {
            if (!celebOpen) return;
            float w = Screen.width / scale, h = Screen.height / scale;
            if (Event.current.type == EventType.Repaint)
            {
                float now = Time.realtimeSinceStartup;
                float dt = Mathf.Min(0.05f, now - lastConfetti);
                lastConfetti = now;
                float since = now - confettiStart;
                if (bursts == 0 || (bursts == 1 && since > 1.6f) || (bursts == 2 && since > 3.4f)) Burst(w, bursts == 0 ? 170 : 90);
                for (int i = bits.Count - 1; i >= 0; i--)
                {
                    Bit b = bits[i];
                    b.vy = Mathf.Min(b.vy + 70f * dt, 160f);
                    b.phase += dt * 3f;
                    b.x += (b.vx + Mathf.Sin(b.phase) * 28f) * dt;
                    b.y += b.vy * dt;
                    if (b.y > h + 20f) { bits.RemoveAt(i); continue; }
                    bits[i] = b;
                }
            }
            if (whiteTex == null) { whiteTex = new Texture2D(1, 1); whiteTex.SetPixel(0, 0, Color.white); whiteTex.Apply(); }
            Color old = GUI.color;
            foreach (Bit b in bits)
            {
                // "flattern": Breite pendelt -> wirkt wie sich drehende Papierschnipsel, bleibt aber pixelig
                float fw = Mathf.Max(2f, Mathf.Round(b.size * Mathf.Abs(Mathf.Cos(b.phase)) / 2f) * 2f);
                GUI.color = b.c;
                GUI.DrawTexture(new Rect(Mathf.Round(b.x), Mathf.Round(b.y), fw, b.size), whiteTex);
            }
            GUI.color = old;
        }

        private void DrawCelebWindow(int id)
        {
            PadWindowBegin(CelebWindowId);
            if (celebBigStyle == null)
            {
                celebBigStyle = new GUIStyle(headerStyle) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fixedHeight = 0, wordWrap = true };
                celebBigStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
                celebTextStyle = new GUIStyle(labelStyle) { fontSize = 16, fixedHeight = 0, wordWrap = true, alignment = TextAnchor.UpperCenter, richText = true };
            }
            if (skinned) GUILayout.Label("GK2 Vanilla+", titleStyle);
            GUILayout.Space(6);
            GUILayout.Label(Labels.T("3.500+ Spieler!", "3,500+ players!"), celebBigStyle);
            GUILayout.Space(6);
            GUILayout.Label(Labels.T(
                "GK2 Vanilla+ wird inzwischen von über 3.500 Leuten gespielt. Damit hätte ich nie gerechnet.\n\nDanke für eure Bewertungen, Kommentare, Fehlermeldungen und Ideen – ganz viele davon stecken inzwischen im Mod, und es kommen noch mehr.",
                "GK2 Vanilla+ is now played by more than 3,500 people. I never expected that.\n\nThank you for your ratings, comments, bug reports and ideas – lots of them are in the mod by now, and more are coming."),
                celebTextStyle);
            GUILayout.Space(4);
            GUILayout.Label("– McFly7", celebTextStyle);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (Btn(Labels.T("Gern geschehen – weiter!", "You're welcome – let's go!"), buttonStyle, GUILayout.Width(300))) Defer(CloseCeleb);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            PadWindowEnd();
            if (skinned && Event.current.type == EventType.Repaint) frameStyle.Draw(new Rect(0, 0, celebWin.width, celebWin.height), false, false, false, false);
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private void DrawCelebration(float scale)
        {
            if (!celebOpen) return;
            if (celebWin.width <= 0) celebWin = new Rect(Screen.width / scale / 2f - 320, Screen.height / scale * 0.18f, 640, 10);
            celebWin = GUILayout.Window(CelebWindowId, celebWin, DrawCelebWindow, skinned ? "" : "GK2 Vanilla+", windowStyle);
        }
    }
}
