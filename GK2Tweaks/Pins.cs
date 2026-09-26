using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GK2Tweaks
{
    // Anpinnen: Rezepte (Werkbaenke), Bauplaene (Baumenue) und Stadtgebaeude bekommen oben rechts eine kleine
    // Pinnadel. Angepinnte Dinge erscheinen in einer Liste am Bildschirmrand mit Haben/Brauchen je Zutat
    // (Inventar des Spielers, ohne Truhen). Eigene Umsetzung, angeregt durch "Recipe Pin" von farfars.
    internal static class Pins
    {
        internal const int Max = 6;

        internal sealed class Need
        {
            public string Id, Name, IconId;
            public bool Group;
            public int Count, Have;
        }

        internal sealed class Pin
        {
            public string Key, Title, IconId;
            public List<Need> Needs = new List<Need>();
            public bool Ready;
        }

        internal static readonly List<Pin> List = new List<Pin>();
        private static float refreshAt;
        private static int lastState = -1;
        private static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        private static readonly Regex Tags = new Regex("<[^>]+>");

        internal static bool IsPinned(string key) => List.Exists(p => p.Key == key);

        internal static void Toggle(Pin p)
        {
            if (p == null) return;
            int i = List.FindIndex(x => x.Key == p.Key);
            if (i >= 0) { List.RemoveAt(i); PinButton.RefreshAll(); return; }
            if (List.Count >= Max) List.RemoveAt(0);
            List.Add(p);
            Refresh();
            PinButton.RefreshAll();
        }

        internal static void Unpin(Pin p) { List.Remove(p); PinButton.RefreshAll(); }

        internal static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : Tags.Replace(s, "").Trim();

        internal static string Loc(string id)
        {
            try { return Clean(LLBase.L(id)); } catch { return id; }
        }

        // Rezept direkt aus der Balance-Definition (Rezeptgitter der Werkbank)
        internal static Pin FromCraftDef(CraftDef def, WgoData wgo)
        {
            string icon = null;
            try { icon = def.GetOutputPreview(wgo).IconId; } catch { }
            var p = new Pin { Key = "craft:" + def.id, Title = Loc(def.id), IconId = icon };
            foreach (NeedItemData n in def.needItems)
            {
                if (n == null || string.IsNullOrEmpty(n.Id)) continue;
                int count;
                try { count = n.GetCount(wgo); } catch { count = 1; }
                string ic = null;
                try
                {
                    if (!n.IsGroup) ic = n.ItemDef?.iconId;
                    else if (n.TryGetGroupItemDefs(out List<ItemDef> defs) && defs != null && defs.Count > 0) ic = defs[0].iconId;
                }
                catch { }
                Need same = p.Needs.Find(x => x.Id == n.Id);
                if (same != null) same.Count += count;
                else p.Needs.Add(new Need { Id = n.Id, Group = n.IsGroup, Count = count, IconId = ic, Name = Loc(n.Id) });
            }
            return p;
        }

        internal static Pin Build(string key, string title, string iconId, List<UICraftItemCellData> cells, WgoData wgo, int multiplier)
        {
            var p = new Pin { Key = key, Title = Clean(title), IconId = iconId };
            if (cells == null) return p;
            foreach (UICraftItemCellData c in cells)
            {
                NeedItemData n = c.initialItem ?? c.currentItem;
                if (n == null || string.IsNullOrEmpty(n.Id)) continue;
                if (c.needDurability > 0f) continue; // Werkzeug-Haltbarkeit, keine Menge
                int count;
                try { count = n.GetCount(wgo) * Math.Max(1, multiplier); } catch { count = 1; }
                NeedItemData shown = c.currentItem ?? n;
                string icon = null;
                try { icon = shown.IsGroup ? null : shown.ItemDef?.iconId; } catch { }
                var need = new Need { Id = n.Id, Group = n.IsGroup, Count = count, IconId = icon, Name = Loc(n.IsGroup ? n.Id : shown.Id) };
                if (need.Group && string.IsNullOrEmpty(need.IconId) && n.TryGetGroupItemDefs(out List<ItemDef> defs) && defs != null && defs.Count > 0)
                    need.IconId = defs[0].iconId;
                Need same = p.Needs.Find(x => x.Id == need.Id);
                if (same != null) same.Count += need.Count; else p.Needs.Add(need);
            }
            return p;
        }

        internal static void Tick()
        {
            MainGame mg = MainGame.Instance;
            int s = mg == null ? -1 : (int)mg.gameState;
            if (s != lastState)
            {
                // Spielstand verlassen: Pins gehoeren zum laufenden Spiel
                if (lastState == (int)MainGame.GameState.InGame) List.Clear();
                lastState = s;
            }
            if (List.Count == 0 || Time.realtimeSinceStartup < refreshAt) return;
            Refresh();
        }

        private static void Refresh()
        {
            refreshAt = Time.realtimeSinceStartup + 0.5f;
            if (!WeekPlan.InGame) return;
            MultiInventory inv;
            try { inv = MainGame.PlayerController.WorkerMultiInventory; } catch { return; }
            foreach (Pin p in List)
            {
                bool ready = true;
                foreach (Need n in p.Needs)
                {
                    int have = 0;
                    try
                    {
                        if (n.Group)
                        {
                            var probe = new NeedItemData(n.Id, 1);
                            if (probe.TryGetGroupItemDefs(out List<ItemDef> defs) && defs != null)
                                foreach (ItemDef d in defs) have += inv.GetTotalCount(d.id);
                        }
                        else have = inv.GetTotalCount(n.Id);
                    }
                    catch { }
                    n.Have = have;
                    if (have < n.Count) ready = false;
                }
                p.Ready = ready;
            }
        }

        internal static Texture2D Icon(string iconId)
        {
            if (string.IsNullOrEmpty(iconId)) return null;
            if (icons.TryGetValue(iconId, out Texture2D t)) return t;
            try
            {
                Sprite s = LazySingletonSO<EasySpritesCollection>.Instance.GetSprite(iconId);
                t = s != null ? GameSkin.Extract(s) : null;
                if (t != null) t.filterMode = FilterMode.Point;
            }
            catch { t = null; }
            icons[iconId] = t;
            return t;
        }
    }

    // Kleine Pinnadel oben rechts auf einem Rezept/Bauplan. Die Widgets werden vom Spiel wiederverwendet,
    // deshalb wird bei jedem Neuzeichnen nur die Funktion ausgetauscht.
    internal sealed class PinButton : MonoBehaviour, IPointerClickHandler
    {
        private static Sprite pinSprite;
        private static readonly List<PinButton> all = new List<PinButton>();
        internal Func<Pins.Pin> Make;
        internal string Key;
        private Image image;

        internal static void Attach(Component widget, string key, Func<Pins.Pin> make, float size = 22f)
        {
            if (widget == null) return;
            PinButton b = null;
            Transform t = widget.transform.Find("GK2VanillaPlus_Pin");
            if (t != null) b = t.GetComponent<PinButton>();
            if (b == null) b = Create(widget.transform as RectTransform);
            if (b == null) return;
            var rt = (RectTransform)b.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-size * 0.6f, -size * 0.6f);
            b.transform.SetAsLastSibling();
            b.Key = key;
            b.Make = make;
            b.gameObject.SetActive(Plugin.PinsEnabled.Value && key != null);
            b.UpdateLook();
        }

        private static PinButton Create(RectTransform parent)
        {
            if (parent == null) return null;
            var go = new GameObject("GK2VanillaPlus_Pin", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-13f, -13f);
            rt.sizeDelta = new Vector2(22f, 22f);
            var img = go.AddComponent<Image>();
            img.sprite = PinSprite();
            img.preserveAspect = true;
            img.raycastTarget = true;
            var b = go.AddComponent<PinButton>();
            b.image = img;
            go.transform.SetAsLastSibling();
            all.Add(b);
            return b;
        }

        internal static void RefreshAll()
        {
            all.RemoveAll(b => b == null);
            foreach (PinButton b in all) { b.gameObject.SetActive(Plugin.PinsEnabled.Value && b.Key != null); b.UpdateLook(); }
        }

        private bool pinned;
        private Canvas canvas;

        private void UpdateLook()
        {
            if (image == null) image = GetComponent<Image>();
            pinned = Key != null && Pins.IsPinned(Key);
            image.color = pinned ? Color.white : new Color(0.85f, 0.8f, 0.72f, 0.8f);
            transform.localRotation = Quaternion.Euler(0, 0, pinned ? 0f : 30f);
            image.enabled = pinned || Hovered();
        }

        // Nicht angepinnt: Nadel nur zeigen, solange die Maus ueber dem Rezept ist (sonst wird das Raster unruhig)
        private bool Hovered()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return false;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(parent, Input.mousePosition, cam);
        }

        private void Update()
        {
            if (image == null) return;
            bool show = pinned || Hovered();
            if (image.enabled != show) image.enabled = show;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            try { Pins.Toggle(Make?.Invoke()); } catch (Exception ex) { Plugin.Log.LogWarning("Pin: " + ex.Message); }
            e.Use();
        }

        // Pixel-Pinnadel (12x12): roter Kopf, heller Glanzpunkt, graue Nadel
        private static Sprite PinSprite()
        {
            if (pinSprite != null) return pinSprite;
            string[] art =
            {
                "....OOOO....",
                "...ORRRRO...",
                "..ORWRRRRO..",
                "..ORRRRRRO..",
                "..ORRRRRDO..",
                "...ORRRDO...",
                "..OORRDDOO..",
                "..ODDDDDDO..",
                "...OOGGOO...",
                ".....GG.....",
                ".....G......",
                ".....G......",
            };
            var tex = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    Color c;
                    switch (art[11 - y][x])
                    {
                        case 'O': c = new Color(0.16f, 0.08f, 0.06f); break;
                        case 'R': c = new Color(0.80f, 0.20f, 0.16f); break;
                        case 'D': c = new Color(0.55f, 0.12f, 0.10f); break;
                        case 'W': c = new Color(1f, 0.85f, 0.75f); break;
                        case 'G': c = new Color(0.72f, 0.72f, 0.76f); break;
                        default: c = new Color(0, 0, 0, 0); break;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            pinSprite = Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 12f);
            return pinSprite;
        }
    }

    [HarmonyPatch(typeof(UICraftWidget), nameof(UICraftWidget.Redraw))]
    internal static class CraftPinPatch
    {
        private static void Postfix(UICraftWidget __instance)
        {
            try
            {
                var d = Traverse.Create(__instance).Field("data").GetValue<UIBaseCraftWidgetData>();
                CraftDef def = d?.CraftDefinition;
                if (def == null || def.isAuto) { PinButton.Attach(__instance, null, null); return; }
                PinButton.Attach(__instance, "craft:" + def.id, () =>
                {
                    string icon = null;
                    try { icon = def.GetOutputPreview(d.WgoData).IconId; } catch { }
                    string title = Pins.Loc(def.id) + (d.CraftsCount > 1 ? " ×" + d.CraftsCount : "");
                    return Pins.Build("craft:" + def.id, title, icon, d.CraftItemCellsData, d.WgoData, d.CraftsCount);
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pin craft: " + e.Message); }
        }
    }

    // Rezeptgitter in der Werkbank
    [HarmonyPatch(typeof(UICraftPreviewItemCell), nameof(UICraftPreviewItemCell.Redraw))]
    internal static class CraftCellPinPatch
    {
        private static void Postfix(UICraftPreviewItemCell __instance)
        {
            try
            {
                var d = Traverse.Create(__instance).Field("data").GetValue<UICraftPreviewItemCellData>();
                CraftDef def = d?.CraftDef;
                if (def == null || d.IsTab || d.IsUnknown || def.isAuto || def.needItems == null || def.needItems.Count == 0) { PinButton.Attach(__instance, null, null, 16f); return; }
                WgoData wgo = d.WgoData;
                PinButton.Attach(__instance, "craft:" + def.id, () => Pins.FromCraftDef(def, wgo), 16f);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pin cell: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UIBuildingWidget), nameof(UIBuildingWidget.Redraw))]
    internal static class BuildPinPatch
    {
        private static void Postfix(UIBuildingWidget __instance)
        {
            try
            {
                var d = Traverse.Create(__instance).Field("data").GetValue<UIBuildingWidgetData>();
                if (d?.BuildData?.Definition == null || d.CraftItemCellsData == null || d.CraftItemCellsData.Count == 0) { PinButton.Attach(__instance, null, null); return; }
                string key = "build:" + d.BuildData.Definition.id;
                PinButton.Attach(__instance, key, () => Pins.Build(key, Pins.Loc(d.Name), d.BuildData.IconId, d.CraftItemCellsData, null, 1));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pin build: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UITownBuildingWidget), nameof(UITownBuildingWidget.Redraw))]
    internal static class TownPinPatch
    {
        private static void Postfix(UITownBuildingWidget __instance)
        {
            try
            {
                var d = Traverse.Create(__instance).Field("data").GetValue<UITownBuildingWidgetData>();
                if (d?.TownBuildingDef == null || d.CraftItemCellsData == null || d.CraftItemCellsData.Count == 0) { PinButton.Attach(__instance, null, null); return; }
                string key = "town:" + d.TownBuildingDef.id;
                PinButton.Attach(__instance, key, () => Pins.Build(key, d.Name, d.TownBuildingDef.iconId, d.CraftItemCellsData, null, 1));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pin town: " + e.Message); }
        }
    }
}
