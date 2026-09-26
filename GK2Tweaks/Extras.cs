using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    // Spielstand-Backups: bevor das Spiel einen Stand ueberschreibt, wird die bisherige Datei mit Zeitstempel gesichert.
    // Liegt im Spielordner (BepInEx/GK2VanillaPlus/Backups), damit Steam Cloud die Backups nicht mit hochlaedt.
    internal static class Backups
    {
        internal sealed class Item
        {
            public string Slot, Dir;
            public DateTime Time;
            public long Bytes;
        }

        private static readonly Dictionary<string, DateTime> lastBackup = new Dictionary<string, DateTime>();
        internal static string Root => Path.Combine(Path.Combine(Paths.BepInExRootPath, "GK2VanillaPlus"), "Backups");
        private const string Stamp = "yyyy-MM-dd_HH-mm-ss";

        internal static void BeforeSave(SaveSlotData slot)
        {
            int keep = Plugin.BackupCount.Value;
            if (keep <= 0 || slot == null || slot.isDemoSave || string.IsNullOrEmpty(slot.slotName)) return;
            DateTime now = DateTime.Now;
            if (lastBackup.TryGetValue(slot.slotName, out DateTime last) && (now - last).TotalMinutes < Plugin.BackupMinutes.Value) return;
            try
            {
                if (Copy(slot.slotName, now.ToString(Stamp)))
                {
                    lastBackup[slot.slotName] = now;
                    Prune(slot.slotName, keep);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Backup: " + e.Message); }
        }

        // kopiert <slot>.dat/.info aus dem Spielstand-Ordner in Backups/<slot>/<name>
        private static bool Copy(string slot, string name)
        {
            string src = SaveSystem.SaveFolder;
            string dat = Path.Combine(src, slot + ".dat");
            if (!File.Exists(dat)) return false;
            string dir = Path.Combine(Path.Combine(Root, slot), name);
            Directory.CreateDirectory(dir);
            File.Copy(dat, Path.Combine(dir, slot + ".dat"), true);
            string info = Path.Combine(src, slot + ".info");
            if (File.Exists(info)) File.Copy(info, Path.Combine(dir, slot + ".info"), true);
            Plugin.Log.LogInfo("Backup: " + slot + " -> " + name);
            return true;
        }

        private static void Prune(string slot, int keep)
        {
            string dir = Path.Combine(Root, slot);
            var dirs = Directory.GetDirectories(dir).Where(d => !Path.GetFileName(d).EndsWith("_restore")).OrderByDescending(d => d).ToList();
            foreach (string old in dirs.Skip(keep))
                try { Directory.Delete(old, true); } catch { }
        }

        internal static List<Item> List(int max = 12)
        {
            var list = new List<Item>();
            try
            {
                if (!Directory.Exists(Root)) return list;
                foreach (string slotDir in Directory.GetDirectories(Root))
                    foreach (string d in Directory.GetDirectories(slotDir))
                    {
                        string slot = Path.GetFileName(slotDir);
                        string dat = Path.Combine(d, slot + ".dat");
                        if (!File.Exists(dat)) continue;
                        string n = Path.GetFileName(d).Replace("_restore", "");
                        DateTime.TryParseExact(n, Stamp, null, System.Globalization.DateTimeStyles.None, out DateTime t);
                        list.Add(new Item { Slot = slot, Dir = d, Time = t, Bytes = new FileInfo(dat).Length });
                    }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Backup list: " + e.Message); }
            return list.OrderByDescending(i => i.Time).Take(max).ToList();
        }

        // Nur im Hauptmenue: aktuellen Stand sichern, dann Backup zurueckkopieren
        internal static bool Restore(Item item, out string message)
        {
            try
            {
                if (MainGame.Instance == null || MainGame.Instance.gameState != MainGame.GameState.MainMenu)
                { message = Labels.T("Wiederherstellen geht nur im Hauptmenü.", "Restoring only works in the main menu."); return false; }
                Copy(item.Slot, DateTime.Now.ToString(Stamp) + "_restore");
                string dst = SaveSystem.SaveFolder;
                File.Copy(Path.Combine(item.Dir, item.Slot + ".dat"), Path.Combine(dst, item.Slot + ".dat"), true);
                string info = Path.Combine(item.Dir, item.Slot + ".info");
                if (File.Exists(info)) File.Copy(info, Path.Combine(dst, item.Slot + ".info"), true);
                Plugin.Log.LogInfo("Backup restored: " + item.Dir);
                message = Labels.T("Wiederhergestellt. Bitte das Spiel einmal neu starten, dann \"Fortsetzen\".",
                                   "Restored. Please restart the game once, then \"Continue\".");
                return true;
            }
            catch (Exception e) { message = e.Message; return false; }
        }
    }

    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.Save))]
    internal static class BackupPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(SaveSlotData slotData)
        {
            if (!Plugin.BenchOn) Backups.BeforeSave(slotData);
        }
    }

    // HUD aus-/einblenden fuer saubere Screenshots - nutzt die Funktion, mit der das Spiel selbst seine Oberflaeche
    // fuer Trailer ausblendet. Esc oder Verlassen des Spiels blendet sie automatisch wieder ein.
    internal static class HudToggle
    {
        internal static bool Hidden { get; private set; }

        internal static void Toggle()
        {
            if (Hidden) { Show(); return; }
            if (!WeekPlan.InGame || GUIElements.Instance == null) return;
            try { GUIElements.Instance.SetVisibilityState(false); Hidden = true; }
            catch (Exception e) { Plugin.Log.LogWarning("HUD: " + e.Message); }
        }

        internal static void Show()
        {
            if (!Hidden) return;
            Hidden = false;
            try { if (GUIElements.Instance != null) GUIElements.Instance.SetVisibilityState(true); } catch { }
        }

        internal static void Tick()
        {
            if (!Hidden) return;
            if (Input.GetKeyDown(KeyCode.Escape) || !WeekPlan.InGame) Show();
        }
    }

    // Screenshot-Taste (Standard F11): wahlweise in 2x/3x/4x Aufloesung, ohne HUD und ohne Mod-Anzeigen.
    internal static class HiResShot
    {
        internal static bool Capturing { get; private set; }
        internal static string Folder => System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus", "Screenshots");

        internal static void Tick()
        {
            if (Capturing || Plugin.ShotKey.Value.MainKey == KeyCode.None || !Plugin.ShotKey.Value.IsDown()) return;
            Plugin.Instance.StartCoroutine(Take());
        }

        internal static System.Collections.IEnumerator Take()
        {
            Capturing = true;
            bool hid = false;
            if (Plugin.ShotHideHud.Value && !HudToggle.Hidden && WeekPlan.InGame) { HudToggle.Toggle(); hid = HudToggle.Hidden; }
            yield return null;
            yield return null;
            string path = null;
            int scale = Mathf.Clamp(Plugin.ShotScale.Value, 1, 4);
            // Unity begrenzt Texturen auf 16384 Pixel Kantenlaenge
            while (scale > 1 && (Screen.width * scale > 16384 || Screen.height * scale > 16384)) scale--;
            try
            {
                System.IO.Directory.CreateDirectory(Folder);
                path = System.IO.Path.Combine(Folder, "GK2_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png");
                ScreenCapture.CaptureScreenshot(path, scale);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Screenshot: " + e.Message); path = null; }
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return null;
            if (hid) HudToggle.Show();
            Capturing = false;
            // Datei wird verzoegert geschrieben - kurz warten, dann melden
            float until = Time.realtimeSinceStartup + 5f;
            while (path != null && !System.IO.File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            if (path != null && System.IO.File.Exists(path))
            {
                ManualSave.Toast(Labels.T("Screenshot gespeichert: ", "Screenshot saved: ") + System.IO.Path.GetFileName(path) + "  (" + Screen.width * scale + "×" + Screen.height * scale + ")", 3f);
                Plugin.Log.LogInfo("Screenshot " + path + " x" + scale);
            }
            else ManualSave.Toast(Labels.T("Screenshot fehlgeschlagen.", "Screenshot failed."), 3f);
        }
    }
}
