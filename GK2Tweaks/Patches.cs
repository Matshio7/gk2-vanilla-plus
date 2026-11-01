using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace GK2Tweaks
{
    // Die PC-Grafikstufen setzen nur einen Teil der Schalter. Hier werden die Werte aus dem Mod-Menue darueber gelegt.
    [HarmonyPatch(typeof(GraphicsTierConfig), nameof(GraphicsTierConfig.ApplyTier))]
    internal static class TierPatch
    {
        // Laeuft beim Start (Grafikstufe anwenden): darf nie werfen, sonst bricht die Init des Spiels ab.
        // Bei einem Fehler bleibt der Eintrag des Spiels unveraendert.
        private static void Postfix(ref PlatformFeatureEntry __result)
        {
            try
            {
                if (__result == null || !SafeMode.On("Graphics") || !Overrides.Any() || GraphicsBench.SuppressOverrides) return;
                PlatformFeatureEntry e = Build(__result);
                if (e != null) __result = e;
            }
            catch (Exception ex) { SafeMode.Fail("Graphics", ex); }
        }

        // Eigene Methode: fehlt Clone() oder ein Feld nach einem Spiel-Update, wirft schon das JIT-Kompilieren -
        // das landet so im try oben statt ausserhalb.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PlatformFeatureEntry Build(PlatformFeatureEntry src)
        {
            PlatformFeatureEntry e = src.Clone();
            if (e == null) return null;
            Overrides.Apply(e);
            return e;
        }
    }

    // Das Spiel setzt VSync und FPS-Limit bei jeder Aenderung der Bildschirmeinstellungen neu.
    [HarmonyPatch(typeof(GameSettings), nameof(GameSettings.ApplyScreenSettings))]
    internal static class ScreenSettingsPatch
    {
        private static string lastLog;

        // vSyncCount vor dem Original merken: so erkennt der Postfix, ob das Spiel ihn ueberhaupt geschrieben hat
        private static void Prefix(out int __state)
        {
            __state = -1;
            try { __state = UnityEngine.QualitySettings.vSyncCount; } catch (Exception e) { SafeMode.Fail("Graphics", e); }
        }

        private static void Postfix(int __state)
        {
            try
            {
                // Zuerst merken, was das Spiel gerade selbst gewaehlt hat - bevor Vanilla+ etwas aendert.
                // Damit laesst ApplyPacing VSync nie aus, wenn das Spiel es an hat (Tearing unter Windows, 1.7.6).
                // Steht danach noch genau der Wert, den Vanilla+ zuletzt selbst gesetzt hat (unveraendert seit vorher),
                // hat das Spiel vSyncCount evtl. gar nicht geschrieben - dann den eigenen Wert NICHT als Spielwahl merken,
                // sonst bliebe VSync z. B. nach dem VSync-Modus fuer die ganze Sitzung "vom Spiel gewuenscht".
                int v = UnityEngine.QualitySettings.vSyncCount;
                bool maybeOwn = __state >= 0 && v == __state && v == Plugin.OwnVsync;
                if (!maybeOwn)
                {
                    Plugin.GameVsync = v;
                    Plugin.GameFps = UnityEngine.Application.targetFrameRate;
                }
                else if (Plugin.GameVsync < 0)
                {
                    // noch kein Spielwert bekannt: nur dem gelesenen VSync-Schalter des Spiels glauben
                    Plugin.GameVsync = (v > 0 && Plugin.GameVsyncSettingOn()) ? v : 0;
                    Plugin.GameFps = UnityEngine.Application.targetFrameRate;
                }
                // sonst: bisherigen Spielwert behalten
                if (!SafeMode.On("Graphics")) return;
                if (GraphicsBench.Running && GraphicsBench.CanRun) GraphicsBench.Unlock(); else Plugin.ApplyPacing();
                // Diagnose: nur bei Aenderung loggen, damit das Log nicht volllaeuft
                string msg = "Screen settings: game vSyncCount " + Plugin.GameVsync + " fps " + Plugin.GameFps
                    + " -> Vanilla+ vSyncCount " + UnityEngine.QualitySettings.vSyncCount + " fps " + UnityEngine.Application.targetFrameRate
                    + ", " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " " + UnityEngine.Screen.fullScreenMode
                    + ", monitor " + Plugin.MonitorHz() + " Hz";
                if (msg != lastLog) { lastLog = msg; Plugin.Log.LogInfo(msg); }
            }
            catch (Exception e) { SafeMode.Fail("Graphics", e); }
        }
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
