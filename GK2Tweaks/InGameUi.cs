using System;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Kleine Mod-Bedienelemente direkt an Fenstern des Spiels:
    // - Zombie-Fenster: Stift neben dem Namen -> umbenennen (eigener Name oder neu wuerfeln)
    // - Handel: Knopf "Daumen-hoch-Waren einlegen" unter dem Verkaufsfeld
    // - Bau-Modus "Abriss": Hinweis auf "Verschieben"
    internal sealed partial class TweaksGui
    {
        // Schnappschuss pro Frame (in Tick), damit OnGUI nicht zwischen Layout und Repaint umschlaegt
        private ZombieWgoData renameZombie;
        private RectTransform renameAnchor;
        private UIZombieWorkerWindow renameWindow;
        private RectTransform renameClose;
        private static Texture2D pencilTex;

        // kleines Pixel-Stift-Symbol (im Stil des Spiels)
        private static Texture2D Pencil()
        {
            if (pencilTex != null) return pencilTex;
            string[] px =
            {
                "......XX.",
                ".....XccX",
                "....XwwX.",
                "...XwwX..",
                "..XwwX...",
                ".XwwX....",
                "XttX.....",
                "XtX......",
                "XX.......",
            };
            int n = px.Length;
            pencilTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    char ch = px[n - 1 - y][x];
                    Color c = ch == 'X' ? new Color(0.23f, 0.16f, 0.12f) : ch == 'w' ? new Color(0.95f, 0.9f, 0.79f) : ch == 'c' ? new Color(0.85f, 0.55f, 0.55f) : ch == 't' ? new Color(0.88f, 0.75f, 0.54f) : new Color(0, 0, 0, 0);
                    pencilTex.SetPixel(x, y, c);
                }
            pencilTex.Apply();
            return pencilTex;
        }
        private bool renaming;
        private string renameText = "";
        private RectTransform tradeAnchor, tradeAnchor2, tradeMoney;
        private bool tradeInPlace;
        private RectTransform tradeDeal;
        private GUIStyle tradeStyle, tradeLabel;
        private Texture2D thumbTex;
        private bool thumbTried;

        private void EnsureTradeStyle()
        {
            if (tradeStyle != null) return;
            tradeStyle = new GUIStyle(skinned ? arrowStyle : buttonStyle) { fixedHeight = 0, fixedWidth = 0, padding = new RectOffset(12, 12, 4, 4) };
            tradeLabel = new GUIStyle(labelStyle) { fontSize = 16, alignment = TextAnchor.MiddleLeft, fixedHeight = 0, wordWrap = false };
            tradeLabel.normal.textColor = new Color(0.96f, 0.92f, 0.8f);
            if (!thumbTried)
            {
                thumbTried = true;
                try
                {
                    Sprite sp = LazyBearTechnology.LazySingletonSO<EasySpritesCollection>.Instance.GetSprite("widget_item_cell_happiness", null);
                    if (sp != null) { thumbTex = GameSkin.Extract(sp); thumbTex.filterMode = FilterMode.Point; }
                }
                catch (Exception e) { Plugin.Log.LogWarning("Trade icon: " + e.Message); }
            }
        }
        private bool tradeCan;
        private float tradeCheckAt;
        private string moveHint;

        internal bool InGameUiWant => renameZombie != null || tradeAnchor != null || moveHint != null;
        internal bool Renaming => renaming && renameZombie != null;

        private void InGameTick()
        {
            renameZombie = null; renameAnchor = null; tradeAnchor = null; tradeAnchor2 = null; tradeMoney = null; moveHint = null;
            if (!WeekPlan.InGame || HudToggle.Hidden) { renaming = false; return; }
            if (Plugin.ZombieRename.Value && SafeMode.On("ZombieRename"))
            {
                try
                {
                    UIZombieWorkerWindow w = TradeHelper.Cached<UIZombieWorkerWindow>();
                    if (w != null && w.IsShown)
                    {
                        var d = Traverse.Create(w).Field("data").GetValue<UIZombieWorkerWindowData>();
                        var lbl = Traverse.Create(w).Field("nameLabel").GetValue<Component>();
                        if (d != null && d.ZombieWgoData != null && lbl != null && lbl.gameObject.activeInHierarchy)
                        {
                            if (lastRenameZombie != d.ZombieWgoData) renaming = false;
                            renameZombie = d.ZombieWgoData; renameAnchor = lbl.transform as RectTransform; renameWindow = w;
                            var cb = Traverse.Create(w).Field("closeButton").GetValue<Component>();
                            renameClose = null;
                            if (cb != null && cb.gameObject.activeInHierarchy)
                            {
                                var im = cb.GetComponentInChildren<UnityEngine.UI.Image>();
                                renameClose = (im != null ? im.transform : cb.transform) as RectTransform;
                            }
                            lastRenameZombie = renameZombie;
                        }
                    }
                }
                catch (Exception e) { SafeMode.Fail("ZombieRename", e); }
            }
            if (renameZombie == null) renaming = false;

            if (Plugin.TradeLikes.Value && SafeMode.On("TradeLikes"))
            {
                try
                {
                    UIVendorWindowData d = TradeHelper.OpenData();
                    if (d != null)
                    {
                        // unter den Knoepfen Abbrechen/Handeln, nicht ueber dem Mengen-Fenster
                        var cw = TradeHelper.Cached<UIItemCountWindow>();
                        bool countOpen = cw != null && cw.IsShown;
                        var vw = Traverse.Create(TradeHelper.Cached<UIVendorWindow>());
                        var b1 = vw.Field("cancelDealBtn").GetValue<Component>();
                        tradeAnchor2 = vw.Field("applyDealBtn").GetValue<Component>()?.transform as RectTransform;
                        tradeMoney = vw.Field("playerMoneyWidget").GetValue<Component>()?.transform as RectTransform;
                        if (!countOpen)
                        {
                            // Controller: das Spiel blendet Abbrechen/Handeln aus -> unser Knopf kommt genau an deren Platz
                            tradeInPlace = b1 != null && !b1.gameObject.activeInHierarchy;
                            if (b1 != null) tradeAnchor = b1.transform as RectTransform;
                            tradeDeal = vw.Field("sellDealInventoryWidget").GetValue<Component>()?.transform as RectTransform;
                        }
                        if (Time.realtimeSinceStartup >= tradeCheckAt)
                        {
                            tradeCheckAt = Time.realtimeSinceStartup + 0.3f;
                            tradeCan = false;
                            foreach (TownVendorProductInfo info in d.Vendor.CurrentTierData.townVendorProductInfos)
                            {
                                if (info == null || info.Definition == null || !d.Vendor.CanBuyItemFromPlayer(info.Definition)) continue;
                                if (TradeHelper.Best(d, info.itemId, MainGame.PlayerData.inventory.Data.GetTotalCountInInventory(info.itemId)) > 0) { tradeCan = true; break; }
                            }
                        }
                    }
                }
                catch (Exception e) { SafeMode.Fail("TradeLikes", e); }
            }

            // Controller: freie Taste im Fenster (meist Y) = Wuerfeln bzw. Daumen-hoch-Waren einlegen
            if (!AnyModWindow)
            {
                try
                {
                    if (renameZombie != null && !renaming && PadExtra.Down(renameWindow)) ZombieRename.Roll(renameZombie, renameWindow);
                    UIVendorWindow vw = tradeAnchor != null ? TradeHelper.Cached<UIVendorWindow>() : null;
                    if (vw != null && PadExtra.Down(vw))
                    {
                        int n = TradeHelper.FillAll();
                        tradeCheckAt = 0;
                        if (n == 0) ManualSave.Toast(Labels.T("Keine passenden Waren im Inventar.", "No matching goods in your inventory."), 2.5f);
                    }
                }
                catch { }
            }

            if (Plugin.MoveObjects.Value && SafeMode.On("MoveObjects") && !BuildHudHint())
            {
                if (BuildMove.Active) moveHint = Labels.T("Vanilla+: Neuen Platz wählen und bauen · Esc/Rechtsklick: abbrechen", "Vanilla+: pick a new spot and place it · Esc/right click: cancel");
                else if (BuildMove.InRemoveMode) moveHint = Labels.T("Vanilla+: Dreh-Taste auf ein Objekt = verschieben", "Vanilla+: rotate key on an object = move it");
            }
        }

        private ZombieWgoData lastRenameZombie;

        // grosse Spiel-Fenster (Charakter/Inventar), hinter denen die Pin-Liste stoeren wuerde
        private static bool BigGameWindowOpen()
        {
            try { var w = TradeHelper.Cached<CharacterWindow>(); return w != null && w.IsShown; } catch { return false; }
        }

        // Hinweis direkt in der Tastenleiste des Bau-Modus unten rechts (gleiche Zeile wie "Drehen"), mit dem
        // Tastensymbol des Spiels. true = erledigt (kein eigener Kasten noetig).
        private readonly System.Collections.Generic.List<GameObject> hudHidden = new System.Collections.Generic.List<GameObject>();
        private void RestoreHudSiblings()
        {
            if (hudHidden.Count == 0) return;
            foreach (GameObject g in hudHidden) if (g != null) g.SetActive(true);
            hudHidden.Clear();
        }

        private bool BuildHudHint()
        {
            try
            {
                bool remove = BuildMove.InRemoveMode, active = BuildMove.Active;
                if (!remove) RestoreHudSiblings();
                if (!remove && !active) return true;
                BuildingHUD hud = LazyUI.Get<BuildingHUD>();
                if (hud == null || !hud.gameObject.activeInHierarchy) return false;
                var t = Traverse.Create(hud);
                var rot = t.Field("rotationHint").GetValue<TMPro.TMP_Text>();
                var rotPad = t.Field("rotationHintGamepad").GetValue<TMPro.TMP_Text>();
                var exit = t.Field("exitHint").GetValue<TMPro.TMP_Text>();
                var exitPad = t.Field("exitHintGamepad").GetValue<TMPro.TMP_Text>();
                if (rot == null || rotPad == null) return false;
                bool changed = false;
                if (remove)
                {
                    string txt = Labels.T("Verschieben", "Move");
                    string icon = ControllerIconLibrary.GetIconId(GameKey.Rotate, null, true);
                    string pad = icon + txt;
                    if (!rot.transform.parent.gameObject.activeSelf) { rot.transform.parent.gameObject.SetActive(true); changed = true; }
                    if (!rotPad.gameObject.activeSelf) { rotPad.gameObject.SetActive(true); changed = true; }
                    // Tastatur: Tastensymbol direkt in den Text (das Symbol-Kaestchen der Zeile bleibt sonst leer)
                    foreach (Transform sib in rot.transform.parent)
                        if (sib != rot.transform && sib.gameObject.activeSelf) { sib.gameObject.SetActive(false); hudHidden.Add(sib.gameObject); changed = true; }
#pragma warning disable 618
                    if (rot.enableWordWrapping) { rot.enableWordWrapping = false; changed = true; }
                    if (rotPad.enableWordWrapping) { rotPad.enableWordWrapping = false; changed = true; }
#pragma warning restore 618
                    if (rot.text != pad) { rot.text = pad; changed = true; }
                    if (rotPad.text != pad) { rotPad.text = pad; changed = true; }
                }
                else if (exit != null && exitPad != null)
                {
                    string txt = Labels.T("Verschieben abbrechen", "Cancel moving");
                    string pad = ControllerIconLibrary.GetIconId(GameKey.Back, null, true) + txt;
                    if (exit.text != txt) { exit.text = txt; changed = true; }
                    if (exitPad.text != pad) { exitPad.text = pad; changed = true; }
                }
                if (changed)
                {
                    foreach (string f in new string[] { "gamepadParent", "mouseParent" })
                    {
                        var go = t.Field(f).GetValue<GameObject>();
                        if (go != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)go.transform);
                    }
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rot.transform.parent);
                    UnityEngine.UI.UIExtensions.RefreshContentFitter((RectTransform)hud.transform);
                }
                return true;
            }
            catch { return false; }
        }

        // Bildschirm-Rechteck eines UI-Elements des Spiels in GUI-Einheiten (GUI.matrix ist skaliert)
        private Rect GuiRect(RectTransform rt, float scale)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Canvas cv = rt.GetComponentInParent<Canvas>();
            Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) / scale, (Screen.height - Mathf.Max(a.y, b.y)) / scale, Mathf.Max(a.x, b.x) / scale, (Screen.height - Mathf.Min(a.y, b.y)) / scale);
        }

        private GUIStyle igButton, igHint, igField;

        private void InGameGUI(float scale)
        {
            if (!InGameUiWant || menuOpen) return;
            if (igButton == null)
            {
                igButton = new GUIStyle(buttonStyle) { fontSize = 14, fixedHeight = 0, padding = new RectOffset(10, 10, 4, 4) };
                igHint = new GUIStyle(overlayStyle) { fontSize = 15, alignment = TextAnchor.MiddleCenter, fixedHeight = 0, wordWrap = false, padding = new RectOffset(12, 12, 6, 6) };
                igField = new GUIStyle(GUI.skin.textField) { fontSize = 16 };
            }

            if (renameZombie != null && renameAnchor != null)
            {
                try
                {
                    Rect r = GuiRect(renameAnchor, scale);
                    if (!renaming)
                    {
                        // quadratisch wie das X des Fensters, direkt links daneben, nur mit Stift-Symbol
                        Rect b;
                        if (renameClose != null)
                        {
                            Rect cr = GuiRect(renameClose, scale);
                            float s = Mathf.Min(cr.width, cr.height);
                            b = new Rect(cr.x - s - 6f, cr.center.y - s / 2f, s, s);
                        }
                        else b = new Rect(r.xMax + 6, r.center.y - 18, 36, 36);
                        bool hit = KitButton(b, null, Labels.T("Umbenennen", "Rename"), KitRed);
                        float isz = Mathf.Round(b.height * 0.5f);
                        GUI.DrawTexture(new Rect(Mathf.Round(b.center.x - isz / 2f), Mathf.Round(b.center.y - isz / 2f), isz, isz), Pencil(), ScaleMode.ScaleToFit);
                        string rn = LazyInput.IsGamepadActive ? PadExtra.Name(renameWindow) : null;
                        if (rn != null)
                        {
                            EnsureKit();
                            var hintR = new Rect(b.x - 70f, b.yMax + 4f, b.width + 70f, 26f);
                            if (Event.current.type == EventType.Repaint) KitFrame(hintR, KitSlate);
                            int ofs = kitText.fontSize; kitText.fontSize = 14;
                            GUI.Label(hintR, "[" + rn + "] " + Labels.T("Neuer Name", "New name"), kitText);
                            kitText.fontSize = ofs;
                        }
                        if (hit)
                        {
                            renaming = true;
                            renameText = LLBase.L(renameZombie.Name);
                            GUI.FocusControl("vp_zname");
                        }
                    }
                    else
                    {
                        EnsureKit();
                        // deckender Kasten unter dem Namen: Eingabefeld + OK / Wuerfeln / Abbrechen
                        float w = 560, h = 64, bh = 38;
                        var box = new Rect(r.center.x - w / 2f, r.yMax + 6f, w, h);
                        if (Event.current.type == EventType.Repaint) KitFrame(box, KitSlate);
                        float y = box.y + (h - bh) / 2f, x = box.x + 12f;
                        if (Event.current.type == EventType.Repaint) KitFrame(new Rect(x, y, 230, bh), new Color(0.07f, 0.07f, 0.09f, 1f));
                        GUI.SetNextControlName("vp_zname");
                        renameText = GUI.TextField(new Rect(x + 2, y + 2, 226, bh - 4), renameText ?? "", 24, kitField);
                        GUI.FocusControl("vp_zname");
                        x += 240;
                        Event ev = Event.current;
                        bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
                        bool esc = ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape;
                        if (KitButton(new Rect(x, y, 70, bh), "OK", Labels.T("Namen übernehmen (Enter)", "Apply name (Enter)"), KitGreen) || enter)
                        {
                            string n = (renameText ?? "").Trim();
                            if (n.Length > 0) ZombieRename.Set(renameZombie, renameWindow, n);
                            renaming = false;
                            if (enter) ev.Use();
                        }
                        else if (KitButton(new Rect(x + 78, y, 104, bh), Labels.T("Würfeln", "Random"), Labels.T("Zufälligen Namen aus dem Spiel", "Random name from the game"), KitGrey))
                        {
                            ZombieRename.Roll(renameZombie, renameWindow);
                            renameText = LLBase.L(renameZombie.Name);
                        }
                        else if (KitButton(new Rect(x + 190, y, 106, bh), Labels.T("Abbruch", "Cancel"), Labels.T("Esc", "Esc"), KitRed) || esc)
                        {
                            renaming = false;
                            if (esc) ev.Use();
                        }
                    }
                }
                catch (Exception e) { renaming = false; SafeMode.Fail("ZombieRename", e); }
            }

            if (tradeAnchor != null)
            {
                try
                {
                    Rect r = GuiRect(tradeAnchor, scale);
                    if (tradeAnchor2 != null) { Rect r2 = GuiRect(tradeAnchor2, scale); r = Rect.MinMaxRect(Mathf.Min(r.xMin, r2.xMin), Mathf.Min(r.yMin, r2.yMin), Mathf.Max(r.xMax, r2.xMax), Mathf.Max(r.yMax, r2.yMax)); }
                    float below = r.yMax;
                    if (tradeMoney != null) below = Mathf.Max(below, GuiRect(tradeMoney, scale).yMax);
                    EnsureTradeStyle();
                    string padN = LazyInput.IsGamepadActive ? PadExtra.Name(TradeHelper.Cached<UIVendorWindow>()) : null;
                    var c = new GUIContent((padN != null ? "[" + padN + "]  " : "") + Labels.T("Daumen-hoch-Waren einlegen", "Add liked goods"), Labels.T("Legt alle Waren, die der Händler mag, in genau der Menge ein, die noch Zufriedenheit bringt.", "Adds all goods the vendor likes, in exactly the amount that still gives happiness."));
                    Vector2 sz = tradeStyle.CalcSize(c);
                    float ic = thumbTex != null ? 26f : 0f;
                    float bw = Mathf.Max(sz.x + ic + 14f, r.width), bh = 40f;
                    Rect b;
                    if (tradeInPlace)
                    {
                        // mittig im Handelsfeld (Pergament), an der Stelle der ausgeblendeten Knoepfe
                        float cxm = r.center.x, maxW = bw;
                        if (tradeDeal != null) { Rect dr = GuiRect(tradeDeal, scale); cxm = dr.center.x; maxW = Mathf.Max(sz.x + ic + 14f, dr.width + 20f); }
                        bw = Mathf.Min(bw, maxW);
                        b = new Rect(cxm - bw / 2f, r.center.y - bh / 2f, bw, bh);
                    }
                    else b = new Rect(r.center.x - bw / 2f, below + 14f, bw, bh);
                    Color oc = GUI.color;
                    if (!tradeCan) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                    bool clicked = GUI.Button(b, GUIContent.none, tradeStyle);
                    float cx0 = b.center.x - (sz.x + ic) / 2f;
                    if (thumbTex != null) GUI.DrawTexture(new Rect(cx0, b.center.y - 12f, 24f, 24f), thumbTex, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(cx0 + ic, b.y, sz.x + 4f, bh), c.text, tradeLabel);
                    GUI.color = oc;
                    if (clicked && tradeCan)
                    {
                        int n = TradeHelper.FillAll();
                        tradeCheckAt = 0;
                        if (n == 0) ManualSave.Toast(Labels.T("Keine passenden Waren im Inventar.", "No matching goods in your inventory."), 2.5f);
                    }
                    else if (clicked) ManualSave.Toast(Labels.T("Keine passenden Waren im Inventar.", "No matching goods in your inventory."), 2.5f);
                }
                catch (Exception e) { SafeMode.Fail("TradeLikes", e); }
            }
            if (moveHint != null)
            {
                var c = new GUIContent(moveHint);
                Vector2 sz = igHint.CalcSize(c);
                GUI.Label(new Rect((Screen.width / scale - sz.x) / 2f, Screen.height / scale - sz.y - 96, sz.x, sz.y), c, igHint);
            }
        }
    }

    internal static class ZombieRename
    {
        internal static void Set(ZombieWgoData z, UIZombieWorkerWindow w, string name)
        {
            // eigener Name: als "gewuerfelt" markiert, damit er nie in den Namens-Topf des Spiels zurueckwandert
            z.SetName(name, true);
            Redraw(w);
            Plugin.Log.LogInfo("Zombie renamed: " + name);
        }

        internal static void Roll(ZombieWgoData z, UIZombieWorkerWindow w)
        {
            z.RollName();
            Redraw(w);
        }

        private static void Redraw(UIZombieWorkerWindow w)
        {
            try { if (w != null) Traverse.Create(w).Method("RedrawName").GetValue(); } catch { }
            try { LazyAudio.PlayAndForget("gui_click"); } catch { }
        }
    }
}
