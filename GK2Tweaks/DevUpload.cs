#if DEV
using System;
using System.IO;
using UnityEngine;

namespace GK2Tweaks
{
    // Dev: Workshop-Upload ohne Klicken - liegt BepInEx/GK2VanillaPlus/upload.now, ruft der Mod im Hauptmenue den
    // Uploader des Spiels (wie Shift+F11 -> Upload) fuer GK2 Vanilla+ auf. Ausgabe im Log mit [UPLOAD].
    internal sealed class DevUpload : MonoBehaviour
    {
        private SteamWorkshopCreatorService svc;
        private float next;

        private static string Flag => Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2VanillaPlus", "upload.now");

        private void Update()
        {
            try
            {
                if (svc != null)
                {
                    svc.Tick();
                    if (!svc.IsBusy) { Plugin.Log.LogInfo("[UPLOAD] finished"); svc.Dispose(); svc = null; }
                    return;
                }
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 2f;
                if (!File.Exists(Flag) || MainGame.Instance == null || MainGame.Instance.gameState == MainGame.GameState.InGame) return;
                if (!SteamManager.Initialized) return;
                File.Delete(Flag);
                svc = new SteamWorkshopCreatorService();
                svc.Logged += m => Plugin.Log.LogInfo("[UPLOAD] " + m);
                SteamWorkshopItemRecord rec = null;
                foreach (SteamWorkshopItemRecord r in svc.Items) if (r.publishedFileId == 3808053878UL) rec = r;
                Plugin.Log.LogInfo("[UPLOAD] start " + (rec != null ? rec.title + " <- " + rec.localFolder : "item not found"));
                if (rec == null) { svc.Dispose(); svc = null; return; }
                svc.Upload(rec);
            }
            catch (Exception e) { Plugin.Log.LogError("[UPLOAD] " + e); try { svc?.Dispose(); } catch { } svc = null; }
        }
    }
}
#endif
