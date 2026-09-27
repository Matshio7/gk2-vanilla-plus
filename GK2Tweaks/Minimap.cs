using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2Tweaks
{
    // Minimap: ein Ausschnitt der gezeichneten Weltkarte des Spiels (Karten-Fenster) in einer Bildschirmecke,
    // mit der eigenen Position. Die Karte wird dafuer gelegentlich einmal in eine Textur gezeichnet
    // (Kopie des Karteninhalts ohne Spiel-Logik, eigene Kamera) - im laufenden Spiel kostet das praktisch nichts.
    // Noch nicht entdeckte Gebiete sind wie im Karten-Fenster des Spiels verdeckt (spoilerfrei).
    internal static class Minimap
    {
        private const int Layer = 31;
        private const float RenderEvery = 300f;
        private static RenderTexture rt;
        private static Vector2 mapSize;
        private static UIMapWindow mapWin;
        private static Texture playerTex;
        private static Rect playerUv;
        private static Vector2 pos;
        private static bool hasPos, rendering, wasInGame;
        private static float nextRender, nextPos;
        private static int knownZones = -1;
        private static IList zonePoints;

        internal static bool Visible => Plugin.MinimapEnabled.Value && SafeMode.On("Minimap") && WeekPlan.InGame && !HudToggle.Hidden
            && !HiResShot.Capturing && rt != null && rt.IsCreated() && hasPos && !(mapWin != null && mapWin.IsShown);

        internal static void Tick()
        {
            bool inGame = WeekPlan.InGame;
            if (inGame != wasInGame) { wasInGame = inGame; knownZones = -1; hasPos = false; }
            if (!Plugin.MinimapEnabled.Value || !inGame) return;
            if (rendering) return;
            int known = MainGame.Instance.GameSave.knowledgeSystem.knownMapZones.Count;
            if (rt == null || !rt.IsCreated() || known != knownZones || Time.unscaledTime >= nextRender)
            {
                knownZones = known;
                nextRender = Time.unscaledTime + RenderEvery;
                Plugin.Instance.StartCoroutine(Render());
                return;
            }
            if (Time.unscaledTime >= nextPos) { nextPos = Time.unscaledTime + 0.1f; UpdatePos(); }
        }

        // Spielerposition auf der Karte (0..1), gleiche Rechnung wie das Karten-Fenster des Spiels
        private static void UpdatePos()
        {
            hasPos = false;
            if (mapSize.x <= 0f) return;
            Vector3 p = MainGame.PlayerData.position.Value;
            Transform a = GUIElements.Instance.WorldMin, b = GUIElements.Instance.WorldMax;
            float x0 = Mathf.Min(a.position.x, b.position.x), x1 = Mathf.Max(a.position.x, b.position.x);
            float z0 = Mathf.Min(a.position.z, b.position.z), z1 = Mathf.Max(a.position.z, b.position.z);
            float z = p.z + p.y * Mathf.Tan(Mathf.PI / 180f * a.rotation.x);
            if (new Rect(x0, z0, x1 - x0, z1 - z0).Contains(new Vector2(p.x, z)))
            {
                pos = new Vector2(Mathf.InverseLerp(a.position.x, b.position.x, p.x), Mathf.InverseLerp(a.position.z, b.position.z, z));
                hasPos = true;
                return;
            }
            // Innenraeume / Unterwelten: fester Punkt auf der Karte (wie im Karten-Fenster)
            var zone = MainGame.PlayerData.CurrentWorldZoneData;
            if (zone == null || zonePoints == null) return;
            foreach (object zp in zonePoints)
            {
                var t = Traverse.Create(zp);
                if (t.Field("worldZoneId").GetValue<string>() != zone.id) continue;
                Vector2 mp = t.Field("mapPosition").GetValue<Vector2>();
                pos = new Vector2(mp.x / mapSize.x + 0.5f, mp.y / mapSize.y + 0.5f);
                hasPos = true;
                return;
            }
        }

        // Karte einmal in die Textur zeichnen
        private static IEnumerator Render()
        {
            rendering = true;
            GameObject holder = null, camGo = null, canvasGo = null;
            bool ok = false;
            try
            {
                mapWin = LazyUI.GetWindow<UIMapWindow>();
                MapPageWidget mpw = mapWin != null ? mapWin.MapPageWidget : null;
                if (mpw == null) { rendering = false; yield break; }
                mpw.Init();
                var tr = Traverse.Create(mpw);
                tr.Method("UpdateZones").GetValue();
                tr.Method("UpdateFightIcons").GetValue();
                ScrollRect sr = tr.Field("scrollRect").GetValue<ScrollRect>();
                RectTransform mapRect = tr.Field("mapRect").GetValue<RectTransform>();
                RectTransform playerIcon = tr.Field("playerIcon").GetValue<RectTransform>();
                zonePoints = tr.Field("worldZonePoints").GetValue<IList>();
                RectTransform content = sr != null ? sr.content : null;
                if (content == null || mapRect == null) { rendering = false; yield break; }
                mapSize = mapRect.rect.size;
                if (mapSize.x < 10f || mapSize.y < 10f) mapSize = mapRect.sizeDelta;

                Image pimg = playerIcon != null ? playerIcon.GetComponentInChildren<Image>(true) : null;
                if (pimg != null && pimg.sprite != null)
                {
                    Texture2D tex = pimg.sprite.texture;
                    Rect r = pimg.sprite.textureRect;
                    playerTex = tex;
                    playerUv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
                }

                float k = Mathf.Min(1f, 2048f / Mathf.Max(mapSize.x, mapSize.y));
                int w = Mathf.Max(64, Mathf.RoundToInt(mapSize.x * k)), h = Mathf.Max(64, Mathf.RoundToInt(mapSize.y * k));
                if (rt == null || rt.width != w || rt.height != h)
                {
                    if (rt != null) rt.Release();
                    rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "GK2VP_Minimap" };
                    rt.Create();
                }

                // Kopie des Karteninhalts - unter einem inaktiven Halter, damit keine Spiel-Skripte starten
                holder = new GameObject("GK2VP_MinimapHolder");
                holder.SetActive(false);
                GameObject clone = Object.Instantiate(content.gameObject, holder.transform);
                if (playerIcon != null && playerIcon.IsChildOf(content))
                {
                    Transform pc = clone.transform.Find(PathFrom(content, playerIcon));
                    if (pc != null) pc.gameObject.SetActive(false);
                }
                Strip(clone);

                camGo = new GameObject("GK2VP_MinimapCam");
                camGo.transform.position = new Vector3(0f, -10000f, 0f);
                Camera cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.085f, 0.07f, 1f);
                cam.cullingMask = 1 << Layer;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.targetTexture = rt;

                canvasGo = new GameObject("GK2VP_MinimapCanvas", typeof(RectTransform));
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;

                var crt = (RectTransform)clone.transform;
                crt.SetParent(canvasGo.transform, false);
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = Vector2.zero;
                crt.localRotation = Quaternion.identity;
                crt.sizeDelta = mapSize;
                crt.localScale = new Vector3(k, k, 1f);
                SetLayer(canvasGo.transform, Layer);
                clone.SetActive(true);
                ok = true;
            }
            catch (System.Exception e) { SafeMode.Fail("Minimap", e); }

            if (ok)
            {
                yield return null; // Canvas einen Frame aufbauen lassen
                try
                {
                    Canvas.ForceUpdateCanvases();
                    camGo.GetComponent<Camera>().Render();
                    Plugin.Log.LogInfo("Minimap: map rendered " + rt.width + "x" + rt.height + " (map " + mapSize + ")");
                }
                catch (System.Exception e) { SafeMode.Fail("Minimap", e); }
            }
            if (canvasGo != null) Object.Destroy(canvasGo);
            if (camGo != null) Object.Destroy(camGo);
            if (holder != null) Object.Destroy(holder);
            rendering = false;
            UpdatePos();
        }

        // Nur Darstellung behalten (Bilder, Texte, Masken), alle Spiel-Skripte und Animationen entfernen
        private static void Strip(GameObject root)
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is CanvasRenderer || c is Graphic || c is Mask || c is RectMask2D || c is CanvasGroup || c is Canvas) continue;
                Object.DestroyImmediate(c);
            }
        }

        private static string PathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        // ---------- Zeichnen (IMGUI, bereits skaliert) ----------
        private static Texture2D dot;

        internal static Rect Draw(float scale, Rect stackOn, float top)
        {
            float s = Plugin.MinimapSize.Value, m = 12f, b = 3f;
            float sw = Screen.width / scale, sh = Screen.height / scale;
            string c = Plugin.MinimapCorner.Value;
            float x = c.EndsWith("Right") ? sw - s - m : m;
            float y = c.StartsWith("Top") ? (stackOn.height > 0 ? stackOn.yMax + 6f : top) : (stackOn.height > 0 ? stackOn.y - 6f - s : sh - s - m);
            var outer = new Rect(x, y, s, s);

            Color old = GUI.color;
            GUI.color = new Color(0.05f, 0.04f, 0.03f, 0.92f);
            GUI.DrawTexture(outer, Texture2D.whiteTexture);
            GUI.color = new Color(0.62f, 0.5f, 0.32f, 1f);
            GUI.DrawTexture(new Rect(x + 1, y + 1, s - 2, s - 2), Texture2D.whiteTexture);
            GUI.color = old;

            var inner = new Rect(x + b, y + b, s - 2 * b, s - 2 * b);
            float spanU = Mathf.Clamp(0.12f * 100f / Mathf.Max(10, Plugin.MinimapZoom.Value), 0.01f, 1f);
            float spanV = Mathf.Clamp(spanU * mapSize.x / Mathf.Max(1f, mapSize.y), 0.01f, 1f);
            float u0 = Mathf.Clamp(pos.x - spanU / 2f, 0f, 1f - spanU), v0 = Mathf.Clamp(pos.y - spanV / 2f, 0f, 1f - spanV);
            GUI.DrawTextureWithTexCoords(inner, rt, new Rect(u0, v0, spanU, spanV));

            float px = inner.x + (pos.x - u0) / spanU * inner.width;
            float py = inner.y + (1f - (pos.y - v0) / spanV) * inner.height;
            if (playerTex != null)
            {
                float ph = 30f, pw = ph * (playerUv.width * playerTex.width) / Mathf.Max(1f, playerUv.height * playerTex.height);
                GUI.DrawTextureWithTexCoords(new Rect(px - pw / 2f, py - ph / 2f, pw, ph), playerTex, playerUv);
            }
            else
            {
                if (dot == null) dot = MakeDot();
                GUI.DrawTexture(new Rect(px - 7, py - 7, 14, 14), dot);
            }
            return outer;
        }

        private static Texture2D MakeDot()
        {
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int yy = 0; yy < 16; yy++)
                for (int xx = 0; xx < 16; xx++)
                {
                    float d = Vector2.Distance(new Vector2(xx + 0.5f, yy + 0.5f), new Vector2(8, 8));
                    t.SetPixel(xx, yy, d < 5.5f ? new Color(0.95f, 0.25f, 0.2f) : d < 7.5f ? new Color(1f, 1f, 1f) : new Color(0, 0, 0, 0));
                }
            t.Apply();
            return t;
        }
    }
}
