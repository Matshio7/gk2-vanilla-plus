using System;
using HarmonyLib;

namespace GK2Tweaks
{
    // Die PC-Grafikstufen setzen nur einen Teil der Schalter. Hier werden die Werte aus dem Mod-Menue darueber gelegt.
    [HarmonyPatch(typeof(GraphicsTierConfig), nameof(GraphicsTierConfig.ApplyTier))]
    internal static class TierPatch
    {
        private static void Postfix(ref PlatformFeatureEntry __result)
        {
            if (__result == null || !Overrides.Any() || GraphicsBench.SuppressOverrides) return;
            PlatformFeatureEntry e = __result.Clone();
            Overrides.Apply(e);
            __result = e;
        }
    }

    // Das Spiel setzt VSync und FPS-Limit bei jeder Aenderung der Bildschirmeinstellungen neu.
    [HarmonyPatch(typeof(GameSettings), nameof(GameSettings.ApplyScreenSettings))]
    internal static class ScreenSettingsPatch
    {
        private static void Postfix() { if (GraphicsBench.Running) GraphicsBench.Unlock(); else Plugin.ApplyPacing(); }
    }

    // Nur im Messmodus: Spielstand wird nie geschrieben.
    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.Save))]
    internal static class SaveBlockPatch
    {
        private static bool Prefix(Action callbackSuccessful)
        {
            if (!Plugin.BenchOn) return true;
            Plugin.Log.LogWarning("[BENCH] Speichern blockiert (Messmodus)");
            try { callbackSuccessful?.Invoke(); } catch (Exception e) { Plugin.Log.LogWarning(e.Message); }
            return false;
        }
    }

    internal static class Overrides
    {
        private const string K = Plugin.Keep;

        internal static bool Any() =>
            Plugin.RenderMode.Value != K || Plugin.Shadows.Value != K || Plugin.Hbao.Value != K ||
            Plugin.PointLights.Value != K || Plugin.BackLight.Value != K || Plugin.Water.Value != K ||
            Plugin.Clouds.Value != K;

        internal static void Apply(PlatformFeatureEntry e)
        {
            switch (Plugin.Shadows.Value)
            {
                case "Off": e.shadowMode = PlatformShadowMode.Off; break;
                case "NGSS_High": SetNgss(e, NgssQualityPreset.High); break;
                case "NGSS_Medium": SetNgss(e, NgssQualityPreset.Medium); break;
                case "NGSS_Low": SetNgss(e, NgssQualityPreset.Low); break;
                case "Unity_Desktop": SetUnity(e, PlatformUnityShadowPreset.DesktopLike); break;
                case "Unity_Balanced": SetUnity(e, PlatformUnityShadowPreset.Balanced); break;
                case "Unity_Performance": SetUnity(e, PlatformUnityShadowPreset.Performance); break;
                case "Unity_Low": SetUnity(e, PlatformUnityShadowPreset.Low); break;
                case "Unity_Minimal": SetUnity(e, PlatformUnityShadowPreset.Minimal); break;
                case "Unity_Console": SetUnity(e, PlatformUnityShadowPreset.Console); break;
            }
            if (Plugin.Hbao.Value != K && Enum.TryParse(Plugin.Hbao.Value, out PlatformHBAOQuality hbao)) e.hbaoQuality = hbao;
            if (Plugin.PointLights.Value != K) e.pointLightMode = Plugin.PointLights.Value == "Faked" ? PlatformPointLightMode.Faked : PlatformPointLightMode.Realtime;
            if (Plugin.BackLight.Value != K) e.backLightEnabled = Plugin.BackLight.Value == "On";
            if (Plugin.Water.Value != K) e.waterTier = Plugin.Water.Value == "Light" ? PlatformWaterTier.Light : PlatformWaterTier.High;
            if (Plugin.Clouds.Value != K) e.cloudAppearance = Plugin.Clouds.Value == "Replaced" ? PlatformCloudAppearance.Replaced : PlatformCloudAppearance.Default;
            if (Plugin.RenderMode.Value != K) e.renderMode = Plugin.RenderMode.Value == "Lightweight" ? PlatformRenderMode.Lightweight : PlatformRenderMode.Native;
        }

        private static void SetNgss(PlatformFeatureEntry e, NgssQualityPreset q)
        {
            e.shadowMode = PlatformShadowMode.NGSS;
            e.ngssQuality = q;
        }

        private static void SetUnity(PlatformFeatureEntry e, PlatformUnityShadowPreset p)
        {
            e.shadowMode = PlatformShadowMode.Unity;
            e.unityShadowPreset = p;
        }
    }
}
