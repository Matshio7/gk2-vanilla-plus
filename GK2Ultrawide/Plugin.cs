using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GK2Ultrawide
{
    [BepInPlugin("mats.gk2.ultrawide", "GK2 Ultrawide", Version)]
    // Weiche Abhaengigkeit: ist GK2 Tweaks da, laedt es zuerst - dann steht der Sichere Start schon fest (siehe SafeStartActive),
    // und sein Log-Hook laeuft, bevor wir etwas patchen. Ohne GK2 Tweaks laedt dieses Plugin ganz normal.
    [BepInDependency("mats.gk2.tweaks", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string Version = "1.0.2";
        internal static ManualLogSource Log;
        internal static ConfigEntry<string> CustomResolution;
        internal static ConfigEntry<string> MainMenuScaleX2;
        internal static ConfigEntry<bool> ScaleSteps;

        // AppDomain-weiter Schluessel: gilt auch, wenn dieselbe DLL aus zwei Ordnern (oder von einem
        // zweiten Loader) geladen wird - statische Felder waeren dann pro Assembly-Kopie getrennt.
        private const string LoadedKey = "mats.gk2.ultrawide.loaded";
        // Von GK2 Tweaks (SafeStart.AppDomainKey) gesetzt: "1" = dieser Start laeuft ohne Vanilla+ (Sicherer Start / linke Umschalttaste)
        private const string SafeStartKey = "mats.gk2.tweaks.safestart";

        internal static bool SafeStartActive()
        {
            try { return (AppDomain.CurrentDomain.GetData(SafeStartKey) as string) == "1"; }
            catch { return false; }
        }

        private void Awake()
        {
            Log = Logger;
            try
            {
                object other = AppDomain.CurrentDomain.GetData(LoadedKey);
                string here = SafeLocation();
                if (other != null)
                {
                    Log.LogWarning("GK2 Ultrawide already loaded from " + other + " - skipping second copy " + here);
                    enabled = false;
                    return;
                }
                AppDomain.CurrentDomain.SetData(LoadedKey, here);
            }
            catch (Exception e) { Log.LogWarning("GK2 Ultrawide: duplicate check failed: " + e.Message); }

            CustomResolution = Config.Bind("General", "CustomResolution", "",
                "Additional resolution(s) for the graphics settings, comma separated, e.g. 3840x1080,5120x1440. Empty = only resolutions reported by the monitor. / Zusaetzliche Aufloesung(en), komma-getrennt. Leer = nur vom Monitor gemeldete.");
            MainMenuScaleX2 = Config.Bind("General", "MainMenuScaleX2", "Auto",
                new ConfigDescription("Main menu scene scaling for unlocked resolutions (Auto/On/Off). Auto = same rule the game uses for similar resolutions. / Skalierung der Hauptmenue-Szene.",
                    new AcceptableValueList<string>("Auto", "On", "Off")));
            ScaleSteps = Config.Bind("General", "ScaleSteps", true,
                "Game 1.011+: adds smaller scale steps (e.g. 3440x1440(x2)) to the game's resolution list for ultrawide resolutions the game only offers at x3/x4 - like the game itself does for 2560x1440. / Ab Spiel 1.011: kleinere Stufen (z. B. 3440x1440(x2)) fuer Ultrawide-Aufloesungen, die das Spiel nur mit x3/x4 anbietet.");
            // Sicherer Start: Vanilla+ ist diesen einen Start komplett aus - dazu gehoert auch dieser Patch.
            // (Greift hier nur, wenn GK2 Tweaks schon geladen hat; sonst prueft der Postfix selbst noch einmal.)
            if (SafeStartActive())
            {
                Log.LogWarning("GK2 Ultrawide: safe start active (Vanilla+ fully off this start) - no patches applied");
                return;
            }
            try
            {
                new Harmony("mats.gk2.ultrawide").PatchAll();
                Log.LogInfo("GK2 Ultrawide " + Version + " loaded (game " + Application.version + ")");
            }
            catch (Exception e)
            {
                // Spiel geaendert (z.B. InitAvailableResolutions umbenannt): Plugin bleibt einfach inaktiv.
                Log.LogWarning("GK2 Ultrawide: game changed, ultrawide unlock skipped (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private string SafeLocation()
        {
            try
            {
                if (Info != null && !string.IsNullOrEmpty(Info.Location)) return Info.Location;
            }
            catch { }
            try { return typeof(Plugin).Assembly.Location; } catch { return "?"; }
        }
    }

    // Das Spiel verwirft in InitAvailableResolutions alle Aufloesungen mit Seitenverhaeltnis > 2:1
    // sowie Breiten > 5120. Wir fuegen diese danach wieder hinzu.
    // Ab Spielversion 1.011 gibt es natives Ultrawide: dann fuegen wir nur noch CustomResolution-Eintraege hinzu.
    // WICHTIG: Dieser Postfix laeuft mitten in der Display-Initialisierung des Spiels. Er darf NIE eine
    // Exception nach aussen werfen, sonst bricht die Init ab (Schwarzbild nach den Logos).
    [HarmonyPatch(typeof(ResolutionConfig), nameof(ResolutionConfig.InitAvailableResolutions))]
    internal static class InitAvailableResolutionsPatch
    {
        private static bool done;
        private static bool warned;
        // Breite/Hoehe eines Eintrags: bis Spielversion 1.010 heissen die Properties ListedWidth/ListedHeight, ab 1.011
        // AppliedWidth/AppliedHeight. Die privaten Felder "width"/"height" gibt es in beiden - deshalb per Reflection.
        private static FieldInfo widthField, heightField;

        private static int Wid(ResolutionConfig r) { return r == null ? 0 : (int)widthField.GetValue(r); }
        private static int Hgt(ResolutionConfig r) { return r == null ? 0 : (int)heightField.GetValue(r); }

        private static void Postfix()
        {
            try
            {
                if (done) return;
                done = true;
                // Sicherer Start (von GK2 Tweaks gesetzt): nichts aendern. Hier statt nur in Awake geprueft - der Postfix laeuft
                // erst, nachdem alle Plugins geladen sind, also unabhaengig von der Ladereihenfolge.
                if (Plugin.SafeStartActive()) { Warn("safe start active (Vanilla+ fully off this start) - ultrawide unlock skipped"); return; }
                // Alle direkten Zugriffe auf Spiel-Member stecken in Apply(): fehlt dort ein Member, wirft
                // schon das JIT-Kompilieren von Apply - das faengt dieser try-Block ab.
                Apply();
            }
            catch (Exception e)
            {
                Warn("ultrawide unlock failed, game resolutions left unchanged (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private static void Warn(string msg)
        {
            if (warned) return;
            warned = true;
            try { Plugin.Log.LogWarning(msg); } catch { }
        }

        private static void Skipped(string what)
        {
            Warn("game changed, ultrawide unlock skipped (missing " + what + ")");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Apply()
        {
            // Vorab pruefen, ob alles noch existiert, was wir brauchen
            FieldInfo hardField = AccessTools.Field(typeof(ResolutionConfig), "hardcodedResolutions");
            FieldInfo availField = AccessTools.Field(typeof(ResolutionConfig), "availableResolutions");
            // Ab 1.011 entfernt (das Spiel berechnet die Hauptmenue-Skalierung selbst) - dann setzen wir sie auch nicht.
            FieldInfo scaleField = AccessTools.Field(typeof(ResolutionConfig), "useMainMenuScaleX2");
            MethodInfo pixelSize = AccessTools.Method(typeof(ResolutionConfig), "GetPixelSize", new[] { typeof(int), typeof(int) });
            MethodInfo tryAdd = AccessTools.Method(typeof(ResolutionConfig), "TryAddAvailableResolution", new[] { typeof(ResolutionConfig) });
            widthField = AccessTools.Field(typeof(ResolutionConfig), "width");
            heightField = AccessTools.Field(typeof(ResolutionConfig), "height");
            if (widthField == null || heightField == null || widthField.FieldType != typeof(int) || heightField.FieldType != typeof(int)) { Skipped("width/height"); return; }
            if (hardField == null || !hardField.IsStatic) { Skipped("hardcodedResolutions"); return; }
            if (availField == null || !availField.IsStatic) { Skipped("availableResolutions"); return; }
            if (tryAdd == null) { Skipped("TryAddAvailableResolution"); return; }

            var hardcoded = hardField.GetValue(null) as List<ResolutionConfig>;
            var available = availField.GetValue(null) as List<ResolutionConfig>;
            if (hardcoded == null) { Skipped("hardcodedResolutions list"); return; }
            if (available == null) { Skipped("availableResolutions list"); return; }
            if (Plugin.CustomResolution == null || Plugin.MainMenuScaleX2 == null) { Skipped("config"); return; }

            var custom = new List<Vector2Int>();
            string customText = Plugin.CustomResolution.Value ?? "";
            foreach (string part in customText.Split(','))
            {
                string[] wh = part.Trim().ToLowerInvariant().Split('x');
                if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h) && w > 0 && h > 0)
                {
                    var v = new Vector2Int(w, h);
                    if (!custom.Contains(v)) custom.Add(v);
                }
            }

            // Natives Ultrawide erkennen: filtert das Spiel nicht mehr, enthaelt seine Liste schon
            // Eintraege mit > 2:1 oder Breite > 5120. Dann nichts selbst freischalten.
            bool native = false;
            foreach (ResolutionConfig r in available)
            {
                int rw = Wid(r), rh = Hgt(r);
                if (r == null || rh <= 0) continue;
                if ((float)rw / rh > 2f || rw > 5120) { native = true; break; }
            }

            if (native)
            {
                if (custom.Count == 0)
                {
                    Plugin.Log.LogInfo("Native ultrawide detected (game " + Application.version + "): nothing to unlock");
                }
                else
                {
                    Plugin.Log.LogInfo("Native ultrawide detected (game " + Application.version + "): only adding CustomResolution entries");
                    foreach (Vector2Int c in custom)
                    {
                        if (Contains(available, c)) continue;
                        // Scale nur setzen, wenn der Nutzer es explizit will - sonst gilt die Regel des Spiels
                        AddResolution(hardcoded, c, pixelSize, scaleField, false);
                    }
                }
                AddScaleSteps(available, scaleField);
                return;
            }

            Plugin.Log.LogInfo("Game filters ultrawide resolutions: unlocking (game " + Application.version + ")");

            var candidates = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions) candidates.Add(new Vector2Int(r.width, r.height));
            if (Display.main != null) candidates.Add(new Vector2Int(Display.main.systemWidth, Display.main.systemHeight));
            candidates.Add(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
            candidates.AddRange(custom);

            // Meldet der Monitor (z.B. unter Wine) keine Aufloesungen, nutzt das Spiel die Hardcoded-Liste als Fallback.
            // Diese Liste uebernehmen, damit unsere Eintraege sie nicht verdraengen.
            if (available.Count == 0)
                foreach (ResolutionConfig r in hardcoded) if (r != null) ResolutionConfig.TryAddAvailableResolution(r);
            var seen = new HashSet<Vector2Int>();
            foreach (Vector2Int c in candidates)
            {
                if (!seen.Add(c) || c.y < 720) continue;
                bool skippedByGame = (float)c.x / c.y > 2f || c.x > 5120;
                if (!skippedByGame && !custom.Contains(c)) continue;
                AddResolution(hardcoded, c, pixelSize, scaleField, true);
            }
            AddScaleSteps(available, scaleField);
        }

        // Ab Spiel 1.011 (dort gibt es useMainMenuScaleX2 nicht mehr) bietet das Spiel Ultrawide nur grob an: 3440x1440,
        // 3840x1080 und 3840x1600 mit x3, 5120x1440 mit x4 - frueher liefen sie mit x2. Wie das Spiel selbst bei 2560x1440
        // ("(x2)"/"(x3)") fuegen wir kleinere Stufen als eigene Listeneintraege direkt hinter dem Eintrag des Spiels ein.
        // Das Spiel speichert die Wahl mit (Breite, Hoehe, Pixelgroesse, Zusatz); ohne GK2 Ultrawide nimmt es wieder seine Stufe.
        // Neue Eintraege ueber den 2-Parameter-Konstruktor (gibt es in allen Versionen) plus Felder per Reflection.
        private static void AddScaleSteps(List<ResolutionConfig> available, FieldInfo scaleField)
        {
            try
            {
                if (scaleField != null || Plugin.ScaleSteps == null || !Plugin.ScaleSteps.Value) return;
                FieldInfo pixField = AccessTools.Field(typeof(ResolutionConfig), "pixelSize");
                FieldInfo tagField = AccessTools.Field(typeof(ResolutionConfig), "customAdditinalString");
                if (pixField == null || tagField == null || pixField.FieldType != typeof(int) || tagField.FieldType != typeof(string))
                {
                    Warn("game changed, scale steps skipped (missing pixelSize/customAdditinalString)");
                    return;
                }
                var added = new List<string>();
                foreach (ResolutionConfig r in available.ToArray())
                {
                    int w = Wid(r), h = Hgt(r);
                    if (r == null || h <= 0 || (float)w / h <= 2f) continue;
                    int p = (int)pixField.GetValue(r);
                    int at = available.IndexOf(r), ins = 0;
                    for (int k = 2; k < p; k++)
                    {
                        string tag = "(x" + k + ")";
                        bool exists = false;
                        foreach (ResolutionConfig o in available)
                            if (o != null && Wid(o) == w && Hgt(o) == h && ((int)pixField.GetValue(o) == k || (tagField.GetValue(o) as string) == tag)) { exists = true; break; }
                        if (exists) continue;
                        var cfg = new ResolutionConfig(w, h);
                        pixField.SetValue(cfg, k);
                        tagField.SetValue(cfg, tag);
                        available.Insert(Math.Min(available.Count, at + 1 + ins), cfg);
                        ins++;
                        added.Add(w + "x" + h + tag);
                    }
                }
                if (added.Count > 0) Plugin.Log.LogInfo("Scale steps added to the resolution list: " + string.Join(", ", added.ToArray()));
            }
            catch (Exception e)
            {
                Warn("scale steps skipped (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private static bool Contains(List<ResolutionConfig> list, Vector2Int c)
        {
            foreach (ResolutionConfig r in list)
                if (r != null && Wid(r) == c.x && Hgt(r) == c.y) return true;
            return false;
        }

        // Ein Eintrag pro Aufloesung; Fehler hier betreffen nur diesen einen Eintrag.
        private static void AddResolution(List<ResolutionConfig> hardcoded, Vector2Int c, MethodInfo pixelSize, FieldInfo scaleField, bool autoScale)
        {
            try
            {
                List<ResolutionConfig> matches = hardcoded.FindAll(r => r != null && Wid(r) == c.x && Hgt(r) == c.y);
                if (matches.Count > 0)
                {
                    foreach (ResolutionConfig m in matches) ResolutionConfig.TryAddAvailableResolution(m);
                }
                else
                {
                    var cfg = new ResolutionConfig(c.x, c.y);
                    if (scaleField != null)
                    {
                        string mode = Plugin.MainMenuScaleX2.Value;
                        if (mode == "On" || mode == "Off")
                        {
                            scaleField.SetValue(cfg, mode == "On");
                        }
                        else if (autoScale && pixelSize != null)
                        {
                            object ps = pixelSize.Invoke(null, new object[] { c.x, c.y });
                            int pix = ps is int ? (int)ps : 0;
                            if (pix > 0) scaleField.SetValue(cfg, c.y / pix >= 600);
                        }
                    }
                    ResolutionConfig.TryAddAvailableResolution(cfg);
                }
                Plugin.Log.LogInfo($"Resolution unlocked: {c.x}x{c.y}");
            }
            catch (Exception e)
            {
                Warn("could not add resolution " + c.x + "x" + c.y + " (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }
    }
}
