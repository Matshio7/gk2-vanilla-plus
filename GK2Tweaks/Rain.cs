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
        private static float next, lastW = 1f, lastH = 1f;

        internal static void Tick()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            try
            {
                float w = 1f, h = 1f;
                if (Plugin.WideRain.Value)
                {
                    Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : null;
                    float aspect = cam != null ? cam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
                    float zoomOut = CameraZoom.Current > 1f && CameraZoom.Current < 100f ? 100f / CameraZoom.Current : 1f;
                    w = Mathf.Max(1f, aspect / (16f / 9f)) * zoomOut;
                    h = zoomOut;
                }
                bool changed = Mathf.Abs(w - lastW) > 0.01f || Mathf.Abs(h - lastH) > 0.01f;
                lastW = w; lastH = h;
                if (w <= 1.01f && h <= 1.01f && done.Count == 0) return;
                foreach (WeatherComponent wc in UnityEngine.Object.FindObjectsByType<WeatherComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var list = paramsField?.GetValue(wc) as ControllableParameterList;
                    if (list?.parameters == null) continue;
                    foreach (ControllableParameter p in list.parameters)
                    {
                        var cp = p as CPParticleEmission;
                        if (cp == null || cp.particleSystem == null) continue;
                        if (!done.TryGetValue(cp.particleSystem, out Orig o))
                        {
                            o = new Orig { scale = cp.particleSystem.shape.scale, rate = (float)defaultField.GetValue(cp), cp = cp };
                            done[cp.particleSystem] = o;
                            changed = true;
                        }
                        if (!changed) continue;
                        ParticleSystem.ShapeModule shape = cp.particleSystem.shape;
                        shape.scale = new Vector3(o.scale.x * w, o.scale.y * h, o.scale.z);
                        defaultField.SetValue(cp, o.rate * w * h);
                        // aktuelle Rate sofort mitziehen (sonst erst beim naechsten Wetterwechsel)
                        ParticleSystem.EmissionModule em = cp.particleSystem.emission;
                        if (em.rateOverTime.constant > 0f && o.rate > 0f)
                            em.rateOverTime = em.rateOverTime.constant / Mathf.Max(0.01f, cur(cp, o)) * w * h;
                        cur(cp, o, w * h);
                    }
                }
            }
            catch (Exception e) { SafeMode.Fail("Rain", e); next = Time.unscaledTime + 10f; }
        }

        // zuletzt angewendeter Faktor je Emitter (fuer das Umrechnen der laufenden Rate)
        private static readonly Dictionary<CPParticleEmission, float> factor = new Dictionary<CPParticleEmission, float>();
        private static float cur(CPParticleEmission cp, Orig o) => factor.TryGetValue(cp, out float f) ? f : 1f;
        private static void cur(CPParticleEmission cp, Orig o, float f) => factor[cp] = f;
    }
}
