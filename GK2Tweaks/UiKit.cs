using UnityEngine;

namespace GK2Tweaks
{
    // Kleine, selbst gezeichnete Pixel-Bedienelemente (klare Kanten, gut lesbar bei jeder Groesse) im Farbstil des Spiels.
    internal sealed partial class TweaksGui
    {
        private static Texture2D kitWhite;
        private GUIStyle kitText, kitField;

        internal static readonly Color KitSlate = new Color(0.16f, 0.17f, 0.22f, 0.98f);
        internal static readonly Color KitRed = new Color(0.71f, 0.19f, 0.16f, 1f);
        internal static readonly Color KitGreen = new Color(0.26f, 0.55f, 0.24f, 1f);
        internal static readonly Color KitGrey = new Color(0.33f, 0.35f, 0.42f, 1f);

        private static void KitFill(Rect r, Color c)
        {
            if (kitWhite == null) { kitWhite = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; kitWhite.SetPixel(0, 0, Color.white); kitWhite.Apply(); }
            Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, kitWhite); GUI.color = o;
        }

        // Kasten: dunkle Kante aussen, helle Kante innen oben (Pixel-Look wie die Spiel-Rahmen)
        internal static void KitFrame(Rect r, Color fill)
        {
            r = new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));
            KitFill(r, new Color(0.06f, 0.05f, 0.06f, 1f));
            var inner = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
            KitFill(inner, fill);
            KitFill(new Rect(inner.x, inner.y, inner.width, 2f), new Color(1f, 1f, 1f, 0.18f));
            KitFill(new Rect(inner.x, inner.yMax - 2f, inner.width, 2f), new Color(0f, 0f, 0f, 0.25f));
        }

        private void EnsureKit()
        {
            if (kitText != null) return;
            kitText = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fixedHeight = 0, fixedWidth = 0, wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            kitText.normal.textColor = new Color(0.97f, 0.93f, 0.82f);
            var dark = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; dark.SetPixel(0, 0, new Color(0.07f, 0.07f, 0.09f, 1f)); dark.Apply();
            kitField = new GUIStyle(GUI.skin.textField) { fontSize = 17, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 2, 2), border = new RectOffset(0, 0, 0, 0) };
            kitField.normal.background = kitField.focused.background = kitField.hover.background = dark;
            kitField.normal.textColor = kitField.focused.textColor = kitField.hover.textColor = new Color(0.97f, 0.93f, 0.82f);
            if (GameSkin.PixelFont != null) kitField.font = GameSkin.PixelFont;
        }

        // Knopf mit Text (oder ohne, dann zeichnet der Aufrufer ein Symbol hinein)
        internal bool KitButton(Rect r, string text, string tip, Color fill, int fontSize = 0)
        {
            EnsureKit();
            bool hover = r.Contains(Event.current.mousePosition);
            Color f = hover ? Color.Lerp(fill, Color.white, 0.15f) : fill;
            if (Event.current.type == EventType.Repaint) KitFrame(r, f);
            if (!string.IsNullOrEmpty(text))
            {
                int old = kitText.fontSize;
                if (fontSize > 0) kitText.fontSize = fontSize;
                GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height), text, kitText);
                kitText.fontSize = old;
            }
            return GUI.Button(r, new GUIContent("", tip), GUIStyle.none);
        }
    }
}
