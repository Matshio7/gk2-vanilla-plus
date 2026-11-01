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
    [BepInPlugin("mats.gk2.ultrawide", "GK2 Ultrawide", "1.0.2")]
    // Weiche Abhaengigkeit: ist GK2 Tweaks da, laedt es zuerst - dann steht der Sichere Start schon fest (siehe SafeStartActive),
    // und sein Log-Hook laeuft, bevor wir etwas patchen. Ohne GK2 Tweaks laedt dieses Plugin ganz normal.
    [BepInDependency("mats.gk2.tweaks", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<string> CustomResolution;
        internal static ConfigEntry<string> MainMenuScaleX2;
        internal static ConfigEntry<string> PixelScaleSetting;

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
            PixelScaleSetting = Config.Bind("General", "PixelScale", "Auto",
                new ConfigDescription("Pixel scaling of the game image. Auto = the game decides (default). 2 = more of the world visible and a smaller interface on large or ultrawide screens (like Vanilla+ before game update 1.011), 3/4 = bigger. Values that do not fit your resolution are ignored (see log). Restart the game after changing. / Pixel-Skalierung des Spielbilds. Auto = Spiel entscheidet. Wirkt nach Neustart.",
                    new AcceptableValueList<string>("Auto", "2", "3", "4")));
            // Fuer das Vanilla+-Menue: Standard "nichts aktiv"; wird unten ueberschrieben, wenn PixelScale installiert ist
            PixelScale.Publish("Auto", "off");
            // Sicherer Start: Vanilla+ ist diesen einen Start komplett aus - dazu gehoert auch dieser Patch.
            // (Greift hier nur, wenn GK2 Tweaks schon geladen hat; sonst prueft der Postfix selbst noch einmal.)
            Harmony harmony = null;
            if (SafeStartActive())
            {
                Log.LogWarning("GK2 Ultrawide: safe start active (Vanilla+ fully off this start) - no patches applied");
                return;
            }
            try
            {
                harmony = new Harmony("mats.gk2.ultrawide");
                harmony.PatchAll();
                Log.LogInfo("GK2 Ultrawide 1.0.2 loaded (game " + Application.version + ")");
            }
            catch (Exception e)
            {
                // Spiel geaendert (z.B. InitAvailableResolutions umbenannt): Plugin bleibt einfach inaktiv.
                Log.LogWarning("GK2 Ultrawide: game changed, ultrawide unlock skipped (" + e.GetType().Name + ": " + e.Message + ")");
            }
            // Pixel-Skalierung: eigene, manuelle Patches in eigenem try - ein fehlender Member darf den Unlock oben nicht mitreissen.
            // Bei "Auto" wird gar nichts gepatcht.
            try
            {
                if (harmony == null) harmony = new Harmony("mats.gk2.ultrawide");
                PixelScale.Install(harmony);
            }
            catch (Exception e)
            {
                Log.LogWarning("Pixel scale: skipped (" + e.GetType().Name + ": " + e.Message + ")");
                PixelScale.Publish("Auto", "skipped");
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
        // Einmal gesucht (EnsureSizeFields), genutzt von Apply() und von PixelScale.AfterInit().
        private static FieldInfo widthField, heightField;
        private static bool sizeLookupDone, sizeFieldsOk;

        // true = "width"/"height" gefunden (Instanzfelder vom Typ int). Wirft nie, sucht nur beim ersten Aufruf.
        internal static bool EnsureSizeFields()
        {
            if (sizeLookupDone) return sizeFieldsOk;
            try
            {
                FieldInfo wf = AccessTools.Field(typeof(ResolutionConfig), "width");
                FieldInfo hf = AccessTools.Field(typeof(ResolutionConfig), "height");
                if (wf != null && hf != null && !wf.IsStatic && !hf.IsStatic && wf.FieldType == typeof(int) && hf.FieldType == typeof(int))
                {
                    widthField = wf;
                    heightField = hf;
                    sizeFieldsOk = true;
                }
            }
            catch { sizeFieldsOk = false; }
            finally { sizeLookupDone = true; }
            return sizeFieldsOk;
        }

        // Nur nach EnsureSizeFields() == true sinnvoll; ohne Felder (oder fuer null) 0.
        internal static int Wid(ResolutionConfig r) { return r == null || widthField == null ? 0 : (int)widthField.GetValue(r); }
        internal static int Hgt(ResolutionConfig r) { return r == null || heightField == null ? 0 : (int)heightField.GetValue(r); }

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
            // Pixel-Skalierung (nur aktiv, wenn installiert): Hauptmenue-Flag der Listeneintraege angleichen. Wirft nie;
            // das zusaetzliche try faengt auch einen Fehler beim JIT-Kompilieren von AfterInit ab (liegt ausserhalb des try oben).
            try { PixelScale.AfterInit(); } catch { }
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
            if (!EnsureSizeFields()) { Skipped("width/height"); return; }
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
                    return;
                }
                Plugin.Log.LogInfo("Native ultrawide detected (game " + Application.version + "): only adding CustomResolution entries");
                foreach (Vector2Int c in custom)
                {
                    if (Contains(available, c)) continue;
                    // Scale nur setzen, wenn der Nutzer es explizit will - sonst gilt die Regel des Spiels
                    AddResolution(hardcoded, c, pixelSize, scaleField, false);
                }
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

    // Optionale Pixel-Skalierung (General/PixelScale = Auto|2|3|4).
    // Seit Spiel-Update 1.011 waehlt das Spiel z.B. fuer 3440x1440 Pixelgroesse 3 (vorher mit Vanilla+ 2).
    // Bekannt aus dem Repo: logische Hoehe = Hoehe / Pixelgroesse (Ganzzahl), Hauptmenue-x2 ab logischer Hoehe >= 600.
    // Drei Ebenen, jede per AccessTools gesucht, null-geprueft und bei Fehlen nur geloggt:
    //  A) Postfix auf static int ResolutionConfig.GetPixelSize(int, int)
    //  B) Postfix auf den Getter von ResolutionConfig.PixelSize, falls es eine statische int-Property ist
    //  C) Nach GameSettings.ApplyScreenSettings: statisches PixelSize-Feld/-Setter korrigieren und die Kamera neu rechnen lassen
    // Alles per Reflection, weil das csproj kein LazyBearTechnology (IntVector2) referenziert.
    // TODO nach Decompile 1.011: falls ResolutionConfig ein Instanzfeld fuer die Pixelgroesse hat (feste Werte in
    // hardcodedResolutions), hier gezielt setzen - Name erst nach Pruefung eintragen, nicht raten.
    internal static class PixelScale
    {
        private const string ValueKey = "mats.gk2.ultrawide.pixelscale";
        private const string StatusKey = "mats.gk2.ultrawide.pixelscale.status";

        private static bool installed;
        private static bool layerA, layerB, layerC;
        private static int hitsA;
        private static bool hitsLogged;
        private static bool busy;        // Rekursionsschutz Getter-Postfix (B)
        private static bool correcting;  // Rekursionsschutz Ebene C (Kamera-Refresh kann erneut ApplyScreenSettings ausloesen)
        private static bool bypass;      // Ebene A kurz aus: AfterInit fragt den Spielwert von GetPixelSize ab
        private static MethodInfo getPixelSize;
        private static readonly HashSet<long> logged = new HashSet<long>();
        private static readonly HashSet<string> warned = new HashSet<string>();

        // Statisches PixelSize als Feld oder Property (was davon existiert, steht erst zur Laufzeit fest)
        private static FieldInfo pixelField;
        private static PropertyInfo pixelProp;

        internal static void Publish(string value, string status)
        {
            try
            {
                AppDomain.CurrentDomain.SetData(ValueKey, value);
                AppDomain.CurrentDomain.SetData(StatusKey, status);
            }
            catch { }
        }

        // 0 = Auto (oder ungueltig), sonst 2..4. Wird live gelesen.
        internal static int Chosen()
        {
            try
            {
                if (Plugin.PixelScaleSetting == null) return 0;
                int s;
                if (int.TryParse((Plugin.PixelScaleSetting.Value ?? "").Trim(), out s) && s >= 2 && s <= 4) return s;
            }
            catch { }
            return 0;
        }

        // Obergrenze 1080 logische Pixel: Schutz davor, dass die Kamera ueber Karten-/Culling-Grenzen hinaus sieht.
        internal static bool Fits(int w, int h, int s)
        {
            if (s <= 0 || w <= 0 || h <= 0) return false;
            int lh = h / s, lw = w / s;
            return lh >= 360 && lw >= 640 && lh <= 1080;
        }

        private static void WarnOnce(string key, string msg)
        {
            try
            {
                lock (warned) { if (!warned.Add(key)) return; }
                Plugin.Log.LogWarning(msg);
            }
            catch { }
        }

        private static void Info(string msg)
        {
            try { Plugin.Log.LogInfo(msg); } catch { }
        }

        internal static void Install(Harmony h)
        {
            int s = Chosen();
            if (Plugin.PixelScaleSetting != null)
                Plugin.PixelScaleSetting.SettingChanged += (o, a) => Info("Pixel scale: setting changed to " + Plugin.PixelScaleSetting.Value + " - restart the game to apply it fully");
            if (s == 0) { Info("Pixel scale: Auto (game decides)"); return; }
            if (Plugin.SafeStartActive()) { Info("Pixel scale: safe start active - skipped"); return; }

            Type rc = typeof(ResolutionConfig);
            HarmonyMethod post;

            // A) GetPixelSize(int, int)
            try
            {
                MethodInfo m = AccessTools.Method(rc, "GetPixelSize", new[] { typeof(int), typeof(int) });
                if (m == null) Info("Pixel scale: layer A skipped (missing ResolutionConfig.GetPixelSize)");
                else if (!m.IsStatic || m.ReturnType != typeof(int)) Info("Pixel scale: layer A skipped (GetPixelSize has unexpected signature)");
                else
                {
                    post = new HarmonyMethod(AccessTools.Method(typeof(PixelScale), nameof(GetPixelSizePostfix)));
                    h.Patch(m, postfix: post);
                    getPixelSize = m;
                    layerA = true;
                }
            }
            catch (Exception e) { Info("Pixel scale: layer A skipped (" + e.GetType().Name + ": " + e.Message + ")"); }

            // B) statische int-Property PixelSize (Getter)
            try
            {
                pixelField = AccessTools.Field(rc, "PixelSize");
                // Nur ein beschreibbares statisches int-Feld taugt fuer Ebene C (const/readonly liesse sich nicht setzen)
                if (pixelField != null && (!pixelField.IsStatic || pixelField.FieldType != typeof(int) || pixelField.IsLiteral || pixelField.IsInitOnly)) pixelField = null;
                if (pixelField == null)
                {
                    PropertyInfo p = AccessTools.Property(rc, "PixelSize");
                    MethodInfo g = p != null ? p.GetGetMethod(true) : null;
                    if (p != null && p.PropertyType == typeof(int) && g != null && g.IsStatic)
                    {
                        pixelProp = p;
                        post = new HarmonyMethod(AccessTools.Method(typeof(PixelScale), nameof(PixelSizeGetterPostfix)));
                        h.Patch(g, postfix: post);
                        layerB = true;
                    }
                    else Info("Pixel scale: layer B skipped (ResolutionConfig.PixelSize is no static int property)");
                }
                else Info("Pixel scale: layer B not needed (ResolutionConfig.PixelSize is a static field)");
            }
            catch (Exception e) { Info("Pixel scale: layer B skipped (" + e.GetType().Name + ": " + e.Message + ")"); }

            // C) Korrektur nach GameSettings.ApplyScreenSettings (laeuft neben dem ScreenSettingsPatch von GK2 Tweaks)
            try
            {
                Type gs = AccessTools.TypeByName("GameSettings");
                MethodInfo m = gs != null ? AccessTools.Method(gs, "ApplyScreenSettings") : null;
                if (m == null) Info("Pixel scale: layer C skipped (missing GameSettings.ApplyScreenSettings)");
                else
                {
                    post = new HarmonyMethod(AccessTools.Method(typeof(PixelScale), nameof(ApplyScreenSettingsPostfix)));
                    h.Patch(m, postfix: post);
                    layerC = true;
                }
            }
            catch (Exception e) { Info("Pixel scale: layer C skipped (" + e.GetType().Name + ": " + e.Message + ")"); }

            installed = layerA || layerB || layerC;
            Info("Pixel scale: " + s + " requested, layers A(GetPixelSize)=" + layerA + " B(PixelSize getter)=" + layerB + " C(after ApplyScreenSettings)=" + layerC
                + (installed ? "" : " - nothing installed, game value is used"));
            LogMembers(rc);
            Publish(installed ? s.ToString() : "Auto", installed ? "ok" : "skipped");
        }

        // Diagnose fuer den Autor: alle Member mit "pixel"/"scale" im Namen (statisch und Instanz)
        private static void LogMembers(Type rc)
        {
            try
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var parts = new List<string>();
                foreach (FieldInfo f in rc.GetFields(all))
                {
                    string n = f.Name.ToLowerInvariant();
                    if (n.Contains("pixel") || n.Contains("scale")) parts.Add((f.IsStatic ? "static " : "") + "field " + f.FieldType.Name + " " + f.Name);
                }
                foreach (PropertyInfo p in rc.GetProperties(all))
                {
                    string n = p.Name.ToLowerInvariant();
                    if (!n.Contains("pixel") && !n.Contains("scale")) continue;
                    MethodInfo g = p.GetGetMethod(true);
                    bool st = g != null && g.IsStatic;
                    parts.Add((st ? "static " : "") + "property " + p.PropertyType.Name + " " + p.Name + (p.GetSetMethod(true) != null ? " {get;set;}" : " {get;}"));
                }
                Info("Pixel scale: ResolutionConfig members: " + (parts.Count == 0 ? "(none)" : string.Join(", ", parts.ToArray())));
            }
            catch (Exception e) { Info("Pixel scale: member scan failed (" + e.Message + ")"); }
        }

        // Positionsparameter __0/__1: die echten Parameternamen sind nicht bekannt.
        private static void GetPixelSizePostfix(int __0, int __1, ref int __result)
        {
            if (bypass) return;
            try
            {
                int s = Chosen();
                if (s <= 0 || s == __result) return;
                if (Plugin.SafeStartActive()) return;
                if (!Fits(__0, __1, s)) { NoFit(__0, __1, s); return; }
                hitsA++;
                LogOnce(__0, __1, __result, s);
                __result = s;
            }
            catch (Exception e) { WarnOnce("A", "Pixel scale: GetPixelSize override failed (" + e.GetType().Name + ": " + e.Message + ")"); }
        }

        private static int getterFrame = -1, getterW, getterH;

        private static void PixelSizeGetterPostfix(ref int __result)
        {
            if (busy) return;
            try
            {
                int s = Chosen();
                if (s <= 0 || s == __result) return;
                if (Plugin.SafeStartActive()) return;
                busy = true;
                // Der Getter kann jeden Frame gelesen werden: Aufloesung hoechstens einmal pro Frame per Reflection holen
                int frame = Time.frameCount;
                if (frame != getterFrame)
                {
                    getterFrame = frame;
                    object res;
                    CurrentResolution(out getterW, out getterH, out res);
                }
                if (!Fits(getterW, getterH, s)) { NoFit(getterW, getterH, s); return; }
                LogOnce(getterW, getterH, __result, s);
                __result = s;
            }
            catch (Exception e) { WarnOnce("B", "Pixel scale: PixelSize override failed (" + e.GetType().Name + ": " + e.Message + ")"); }
            finally { busy = false; }
        }

        private static void ApplyScreenSettingsPostfix()
        {
            try
            {
                if (!hitsLogged)
                {
                    hitsLogged = true;
                    Info("Pixel scale: GetPixelSize overrides so far: " + hitsA + (layerA && hitsA == 0 ? " (if the game value is still used, the game may not call GetPixelSize for this resolution)" : ""));
                }
                Correct();
            }
            catch (Exception e) { WarnOnce("C", "Pixel scale: correction after ApplyScreenSettings failed (" + e.GetType().Name + ": " + e.Message + ")"); }
        }

        private static void NoFit(int w, int h, int s)
        {
            long k = ((long)w << 32) ^ (uint)h ^ (1L << 62);
            lock (logged) { if (!logged.Add(k)) return; }
            Info("Pixel scale: " + s + " too large/small for " + w + "x" + h + ", using game value");
        }

        private static void LogOnce(int w, int h, int game, int s)
        {
            long k = ((long)w << 32) ^ (uint)h;
            lock (logged) { if (!logged.Add(k)) return; }
            Info("Pixel scale: " + w + "x" + h + " game=" + game + " -> " + s);
        }

        // Aktuelle Aufloesung: GameSettings.Instance.GetResolutionIntVector2() per Reflection, sonst Screen.width/height.
        // res = das IntVector2-Objekt (fuer CameraSystem.OnResolutionChanged), oder null.
        private static void CurrentResolution(out int w, out int h, out object res)
        {
            w = 0; h = 0; res = null;
            try
            {
                // Typ-/Methodensuche nur einmal (TypeByName durchsucht alle Assemblies)
                if (!resLookupDone)
                {
                    resLookupDone = true;
                    gsType = AccessTools.TypeByName("GameSettings");
                    getRes = gsType != null ? AccessTools.Method(gsType, "GetResolutionIntVector2", Type.EmptyTypes) : null;
                    if (getRes == null) Info("Pixel scale: GameSettings.GetResolutionIntVector2 not found, using Screen size");
                }
                object gs = getRes != null ? StaticInstance(gsType) : null;
                MethodInfo get = gs != null ? getRes : null;
                if (get != null)
                {
                    res = get.Invoke(gs, null);
                    if (res != null)
                    {
                        w = ReadInt(res, "x");
                        h = ReadInt(res, "y");
                    }
                }
            }
            catch { res = null; w = 0; h = 0; }
            if (w <= 0 || h <= 0) { w = Screen.width; h = Screen.height; }
            // Fenstermodus: nie groesser als das echte Fenster rechnen, sonst koennte ein kleines Fenster
            // unter 360 logische Pixel Hoehe fallen, obwohl die eingestellte Aufloesung passt
            else if (Screen.width > 0 && Screen.height > 0) { w = Math.Min(w, Screen.width); h = Math.Min(h, Screen.height); }
        }

        private static bool resLookupDone;
        private static Type gsType;
        private static MethodInfo getRes;

        private static object StaticInstance(Type t)
        {
            if (t == null) return null;
            try
            {
                PropertyInfo p = AccessTools.Property(t, "Instance");
                if (p != null) { MethodInfo g = p.GetGetMethod(true); if (g != null && g.IsStatic) return g.Invoke(null, null); }
            }
            catch { }
            try
            {
                FieldInfo f = AccessTools.Field(t, "Instance");
                if (f != null && f.IsStatic) return f.GetValue(null);
            }
            catch { }
            return null;
        }

        private static int ReadInt(object o, string name)
        {
            Type t = o.GetType();
            FieldInfo f = AccessTools.Field(t, name);
            if (f != null) { object v = f.GetValue(o); if (v is int) return (int)v; }
            PropertyInfo p = AccessTools.Property(t, name);
            if (p != null && p.GetGetMethod(true) != null) { object v = p.GetValue(o, null); if (v is int) return (int)v; }
            return 0;
        }

        // Ebene C: statisches PixelSize setzen (Feld oder Property mit Setter) und Kamera neu rechnen lassen.
        // Kamera nur anstossen, wenn wir wirklich etwas geaendert haben (kein doppeltes Neuberechnen).
        private static void Correct()
        {
            if (!installed || correcting) return;
            int s = Chosen();
            if (s <= 0 || Plugin.SafeStartActive()) return;
            correcting = true;
            try
            {
                int w, h;
                object res;
                CurrentResolution(out w, out h, out res);
                if (!Fits(w, h, s)) { NoFit(w, h, s); return; }
                bool changed = false;
                if (pixelField != null)
                {
                    object cur = pixelField.GetValue(null);
                    if (cur is int && (int)cur != s)
                    {
                        pixelField.SetValue(null, s);
                        LogOnce(w, h, (int)cur, s);
                        changed = true;
                    }
                }
                else if (pixelProp != null)
                {
                    MethodInfo set = pixelProp.GetSetMethod(true);
                    MethodInfo get = pixelProp.GetGetMethod(true);
                    if (set != null && set.IsStatic && get != null)
                    {
                        // busy kurz setzen: der eigene Getter-Postfix (B) greift dann nicht, wir sehen den Spielwert
                        object cur;
                        busy = true;
                        try { cur = get.Invoke(null, null); }
                        finally { busy = false; }
                        if (cur is int && (int)cur != s)
                        {
                            set.Invoke(null, new object[] { s });
                            LogOnce(w, h, (int)cur, s);
                            changed = true;
                        }
                    }
                }
                if (changed && res != null) RefreshCamera(res);
            }
            finally { correcting = false; }
        }

        private static Type csType;

        private static void RefreshCamera(object res)
        {
            try
            {
                if (csType == null) csType = AccessTools.TypeByName("CameraSystem");
                object cs = StaticInstance(csType);
                if (cs == null) return;
                MethodInfo m = AccessTools.Method(csType, "OnResolutionChanged", new[] { res.GetType() });
                if (m == null) { WarnOnce("cam", "Pixel scale: camera refresh skipped (missing CameraSystem.OnResolutionChanged)"); return; }
                m.Invoke(cs, new[] { res });
            }
            catch (Exception e) { WarnOnce("cam", "Pixel scale: camera refresh failed (" + e.GetType().Name + ": " + e.Message + ")"); }
        }

        // Nach InitAvailableResolutions: bei MainMenuScaleX2 = Auto das Hauptmenue-Flag der Listeneintraege an die
        // gewaehlte Skalierung angleichen (gleiche Regel wie im Unlock: logische Hoehe >= 600). Wirft nie.
        // Nur Eintraege, bei denen das Spiel selbst eine andere Skalierung nimmt - wo die Skalierung gleich bleibt,
        // bleibt auch das Hauptmenue-Flag des Spiels. Spielwert unbekannt (Ebene A fehlt) -> Eintrag nicht anfassen.
        // Ab Spiel 1.011 gibt es useMainMenuScaleX2 nicht mehr (das Spiel rechnet die Hauptmenue-Skalierung selbst):
        // dann ist das hier ein No-op mit einer Info-Zeile. Breite/Hoehe ueber die Felder width/height (beide Versionen).
        internal static void AfterInit()
        {
            try
            {
                if (!installed) return;
                int s = Chosen();
                if (s <= 0 || Plugin.SafeStartActive()) return;
                FieldInfo scaleField = AccessTools.Field(typeof(ResolutionConfig), "useMainMenuScaleX2");
                if (scaleField == null)
                {
                    Info("Pixel scale: main menu flag sync not needed (no ResolutionConfig.useMainMenuScaleX2, the game scales the main menu itself)");
                    return;
                }
                if (scaleField.IsStatic || scaleField.FieldType != typeof(bool))
                {
                    Info("Pixel scale: main menu flag sync skipped (ResolutionConfig.useMainMenuScaleX2 has unexpected type)");
                    return;
                }
                if (Plugin.MainMenuScaleX2 == null || Plugin.MainMenuScaleX2.Value != "Auto") return;
                FieldInfo availField = AccessTools.Field(typeof(ResolutionConfig), "availableResolutions");
                if (availField == null || !availField.IsStatic)
                {
                    Info("Pixel scale: main menu flag sync skipped (missing ResolutionConfig.availableResolutions)");
                    return;
                }
                if (!InitAvailableResolutionsPatch.EnsureSizeFields())
                {
                    Info("Pixel scale: main menu flag sync skipped (missing ResolutionConfig width/height fields)");
                    return;
                }
                var available = availField.GetValue(null) as List<ResolutionConfig>;
                if (available == null) return;
                if (getPixelSize == null)
                {
                    Info("Pixel scale: main menu flag sync skipped (game scale per resolution unknown, GetPixelSize not patched)");
                    return;
                }
                int n = 0;
                foreach (ResolutionConfig r in available)
                {
                    if (r == null) continue;
                    int rw = InitAvailableResolutionsPatch.Wid(r), rh = InitAvailableResolutionsPatch.Hgt(r);
                    if (!Fits(rw, rh, s)) continue;
                    int game;
                    bypass = true;
                    try { game = (int)getPixelSize.Invoke(null, new object[] { rw, rh }); }
                    catch (Exception e) { WarnOnce("init-game", "Pixel scale: game scale lookup failed (" + e.GetType().Name + ": " + e.Message + ") - main menu flags left as they are"); return; }
                    finally { bypass = false; }
                    if (game == s) continue;
                    bool want = rh / s >= 600;
                    object cur = scaleField.GetValue(r);
                    if (cur is bool && (bool)cur == want) continue;
                    scaleField.SetValue(r, want);
                    n++;
                }
                if (n > 0) Info("Pixel scale: main menu scaling adjusted on " + n + " resolution entr" + (n == 1 ? "y" : "ies"));
            }
            catch (Exception e) { WarnOnce("init", "Pixel scale: main menu flag sync failed (" + e.GetType().Name + ": " + e.Message + ")"); }
        }
    }
}
