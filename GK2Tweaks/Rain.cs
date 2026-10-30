using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GK2Tweaks
{
    // Regen/Schnee ueber die ganze Bildbreite: Die Wetter-Partikel des Spiels sind fuer 16:9 ausgelegt und
    // hoeren auf Ultrawide-Bildschirmen (und beim Herauszoomen) an den Seiten auf. Wir verbreitern die Emitter
    // passend zum Bildformat und Zoom und erhoehen die Partikelrate im gleichen Verhaeltnis (gleiche Dichte).
    // Idee: "GK2 Ultrawide Rain Fix" von Dry Bones (eigene Umsetzung, ohne Patch des Spiels - reines Nachstellen).
    internal static class Rain
    {
        private sealed class Orig { public Vector3 scale; public float rate; public CPParticleEmission cp; }
        private static readonly Dictionary<ParticleSystem, Orig> done = new Dictionary<ParticleSystem, Orig>();
        private static readonly FieldInfo paramsField = typeof(WeatherComponent).GetField("parameters", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo defaultField = typeof(CPParticleEmission).GetField("defaultValue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static float next, lastW = 1f, lastH = 1f, lastA = 1f;
        // 1.7.3 (GitHub #1, byronlai99): FindObjectsByType lief jede Sekunde und machte regelmaessige Ruckler.
        // Jetzt nur noch suchen, wenn eine Szene geladen wurde (kurz danach und noch einmal etwas spaeter),
        // sonst nur die gemerkten Emitter anpassen - und das auch nur, wenn sich Breite/Zoom/Menge aendern.
        private static readonly List<CPParticleEmission> known = new List<CPParticleEmission>();
        private static readonly List<float> scanAt = new List<float> { 0f };
        private static bool hooked;

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            float t = Time.unscaledTime;
            scanAt.Clear(); scanAt.Add(t + 1f); scanAt.Add(t + 5f);
        }

        // Neuer Wetter-Emitter mitten in einer Szene (WeatherComponent.Awake): kurz danach einmal suchen
        internal static void ScanSoon() { scanAt.Add(Time.unscaledTime + 1f); }

        internal static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            try
            {
                if (!hooked) { UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded; hooked = true; }
                float w = 1f, h = 1f;
                if (Plugin.WideRain.Value)
                {
                    Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : null;
                    float aspect = cam != null ? cam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
                    float zoomOut = CameraZoom.Current > 1f && CameraZoom.Current < 100f ? 100f / CameraZoom.Current : 1f;
                    w = Mathf.Max(1f, aspect / (16f / 9f)) * zoomOut;
                    h = zoomOut;
                }
                // Menge (Saisuke: Regen kostet auf schwachen PCs viel Leistung) - wirkt nur auf die Rate, nicht auf die Breite
                float amount = Mathf.Clamp01(Plugin.RainAmount.Value / 100f);
                bool changed = Mathf.Abs(w - lastW) > 0.01f || Mathf.Abs(h - lastH) > 0.01f || Mathf.Abs(amount - lastA) > 0.01f;
                lastW = w; lastH = h; lastA = amount;
                // Nichts zu tun: Originalwerte einmal zuruecksetzen, Merkliste leeren, keine Suche mehr
                if (w <= 1.01f && h <= 1.01f && amount >= 0.99f)
                {
                    if (done.Count > 0) RestoreAll();
                    return;
                }
                bool scan = false;
                for (int i = scanAt.Count - 1; i >= 0; i--)
                    if (Time.unscaledTime >= scanAt[i]) { scanAt.RemoveAt(i); scan = true; }
                if (scan) Scan();
                foreach (CPParticleEmission cp in known)
                {
                    if (cp == null || cp.particleSystem == null || !done.TryGetValue(cp.particleSystem, out Orig o)) continue;
                    if (!changed && factor.ContainsKey(cp)) continue;
                    Apply(cp, o, w * h * amount, w, h);
                }
            }
            catch (Exception e) { SafeMode.Fail("Rain", e); next = Time.unscaledTime + 10f; }
        }

        // Wetter-Emitter der geladenen Szenen suchen (teuer - nur nach einem Szenenwechsel)
        private static void Scan()
        {
            known.Clear();
            var gone = new List<ParticleSystem>();
            foreach (ParticleSystem ps in done.Keys) if (ps == null) gone.Add(ps);
            foreach (ParticleSystem ps in gone) done.Remove(ps);
            var deadCp = new List<CPParticleEmission>();
            foreach (CPParticleEmission c in factor.Keys) if (c == null || c.particleSystem == null) deadCp.Add(c);
            foreach (CPParticleEmission c in deadCp) factor.Remove(c);
            foreach (WeatherComponent wc in UnityEngine.Object.FindObjectsByType<WeatherComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var list = paramsField?.GetValue(wc) as ControllableParameterList;
                if (list?.parameters == null) continue;
                foreach (ControllableParameter p in list.parameters)
                {
                    var cp = p as CPParticleEmission;
                    if (cp == null || cp.particleSystem == null) continue;
                    if (!done.ContainsKey(cp.particleSystem))
                        done[cp.particleSystem] = new Orig { scale = cp.particleSystem.shape.scale, rate = (float)defaultField.GetValue(cp), cp = cp };
                    known.Add(cp);
                }
            }
        }

        private static void Apply(CPParticleEmission cp, Orig o, float f, float w, float h)
        {
            ParticleSystem.ShapeModule shape = cp.particleSystem.shape;
            shape.scale = new Vector3(o.scale.x * w, o.scale.y * h, o.scale.z);
            defaultField.SetValue(cp, o.rate * f);
            // aktuelle Rate sofort mitziehen (sonst erst beim naechsten Wetterwechsel)
            ParticleSystem.EmissionModule em = cp.particleSystem.emission;
            float prev = cur(cp, o);
            if (f <= 0f) em.rateOverTime = 0f;
            else if (prev <= 0f) { if (o.rate > 0f) em.rateOverTime = o.rate * f; }
            else if (em.rateOverTime.constant > 0f && o.rate > 0f)
                em.rateOverTime = em.rateOverTime.constant / prev * f;
            if (f <= 0f && prev > 0f) cp.particleSystem.Clear();
            cur(cp, o, f);
        }

        // Zurueck auf 100 % / 16:9: Originalwerte wiederherstellen und alles vergessen
        private static void RestoreAll()
        {
            foreach (KeyValuePair<ParticleSystem, Orig> kv in done)
            {
                if (kv.Key == null || kv.Value.cp == null) continue;
                try { Apply(kv.Value.cp, kv.Value, 1f, 1f, 1f); } catch { }
            }
            done.Clear(); factor.Clear(); known.Clear();
            scanAt.Clear(); scanAt.Add(0f);   // beim naechsten Einschalten sofort wieder suchen
        }

        // zuletzt angewendeter Faktor je Emitter (fuer das Umrechnen der laufenden Rate)
        private static readonly Dictionary<CPParticleEmission, float> factor = new Dictionary<CPParticleEmission, float>();
        private static float cur(CPParticleEmission cp, Orig o) => factor.TryGetValue(cp, out float f) ? f : 1f;
        private static void cur(CPParticleEmission cp, Orig o, float f) => factor[cp] = f;
    }

    [HarmonyLib.HarmonyPatch(typeof(WeatherComponent), "Awake")]
    internal static class RainScanPatch
    {
        private static void Postfix() { try { Rain.ScanSoon(); } catch { } }
    }
}
