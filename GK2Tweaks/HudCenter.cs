using System.Collections.Generic;
using UnityEngine;

namespace GK2Tweaks
{
    // HUD zur Mitte (Ultrawide): Auf 21:9 / 32:9 stehen HUD, Gebietsname und NPC-Fenster ganz am Bildschirmrand.
    // Mit der Option rueckt alles in den 16:9-Bereich in der Mitte (wie auf einem normalen Bildschirm),
    // die Spielwelt bleibt ultrabreit. Originalwerte werden gemerkt und beim Ausschalten wiederhergestellt.
    internal static class HudCenter
    {
        private sealed class Orig { public Vector2 aMin, aMax, pos; public bool squeeze; }
        private static readonly Dictionary<RectTransform, Orig> changed = new Dictionary<RectTransform, Orig>();
        private static float next, applied = -1f;
        private static HUD hud;
        private static bool logged;

        // Anteil der Bildbreite links und rechts ausserhalb von 16:9 (0 = kein Ultrawide / aus)
        internal static float Pad
        {
            get
            {
                if (!Plugin.HudCenter.Value || !WeekPlan.InGame) return 0f;
                float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                return aspect > 16f / 9f + 0.02f ? (1f - (16f / 9f) / aspect) / 2f : 0f;
            }
        }

        // Einrueckung fuer die eigenen Anzeigen (FPS, Pins) in GUI-Einheiten
        internal static float Inset(float scale) => Pad * Screen.width / scale;

        internal static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            float pad = Pad;
            if (pad <= 0f) { Restore(); applied = -1f; return; }
            if (hud == null) { Restore(); hud = Object.FindFirstObjectByType<HUD>(); applied = -1f; if (hud == null) return; }
            if (Mathf.Abs(pad - applied) < 0.001f) return;
            Restore();
            applied = pad;
            ApplyContainer(hud.transform as RectTransform, pad);
            GUIElements g = GUIElements.Instance;
            if (g != null)
            {
                if (g.NpcWidget != null) EdgeShift(g.NpcWidget.transform, pad);
                var wz = HarmonyLib.Traverse.Create(g).Field("worldZoneWidget").GetValue<Component>();
                if (wz != null) EdgeShift(wz.transform, pad);
            }
            if (!logged) { logged = true; Plugin.Log.LogInfo("HUD center: " + changed.Count + " element(s) moved into 16:9 (pad " + pad.ToString("0.000") + ")"); }
        }

        // HUD-Container: gestreckt -> in der Breite auf 16:9 zusammenziehen, sonst die Kinder an den Raendern verschieben
        private static void ApplyContainer(RectTransform rt, float pad)
        {
            if (rt == null) return;
            if (rt.GetComponent<Canvas>() == null && Stretched(rt)) { Squeeze(rt, pad); return; }
            for (int i = 0; i < rt.childCount; i++)
            {
                var c = rt.GetChild(i) as RectTransform;
                if (c == null) continue;
                if (Stretched(c)) Squeeze(c, pad);
                else EdgeShift(c, pad);
            }
        }

        private static bool Stretched(RectTransform rt) => rt.anchorMin.x <= 0.001f && rt.anchorMax.x >= 0.999f;

        private static void Squeeze(RectTransform rt, float pad)
        {
            if (changed.ContainsKey(rt)) return;
            changed[rt] = new Orig { aMin = rt.anchorMin, aMax = rt.anchorMax, pos = rt.anchoredPosition, squeeze = true };
            rt.anchorMin = new Vector2(pad, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f - pad, rt.anchorMax.y);
        }

        // Element (oder ein Elternteil) haengt am linken/rechten Rand -> um den Rand-Abstand nach innen schieben
        private static void EdgeShift(Transform t, float pad)
        {
            for (Transform a = t; a != null; a = a.parent)
                if (a is RectTransform ar && changed.TryGetValue(ar, out Orig o) && o.squeeze) return; // liegt schon im zusammengezogenen HUD
            for (int depth = 0; t != null && depth < 6; depth++, t = t.parent)
            {
                var rt = t as RectTransform;
                if (rt == null || rt.GetComponent<Canvas>() != null) return;
                if (changed.ContainsKey(rt)) return;
                bool left = rt.anchorMin.x <= 0.001f && rt.anchorMax.x <= 0.001f;
                bool right = rt.anchorMin.x >= 0.999f && rt.anchorMax.x >= 0.999f;
                if (!left && !right) continue;
                Canvas cv = rt.GetComponentInParent<Canvas>();
                if (cv == null) return;
                float w = ((RectTransform)cv.rootCanvas.transform).rect.width;
                changed[rt] = new Orig { aMin = rt.anchorMin, aMax = rt.anchorMax, pos = rt.anchoredPosition };
                rt.anchoredPosition += new Vector2((left ? 1f : -1f) * pad * w, 0f);
                return;
            }
        }

        private static void Restore()
        {
            if (changed.Count == 0) return;
            foreach (var kv in changed)
            {
                if (kv.Key == null) continue;
                kv.Key.anchorMin = kv.Value.aMin;
                kv.Key.anchorMax = kv.Value.aMax;
                kv.Key.anchoredPosition = kv.Value.pos;
            }
            changed.Clear();
        }
    }
}
