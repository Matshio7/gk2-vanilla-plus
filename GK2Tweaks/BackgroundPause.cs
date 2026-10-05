using UnityEngine;

namespace GK2Tweaks
{
    // "Im Hintergrund pausieren": frueher ueber Application.runInBackground = false. Dabei verpasst Rewired aber
    // Geraete-Ereignisse und der Controller war nach Alt-Tab / Windows-Taste weg, bis man ihn neu einsteckt (gl7).
    // Jetzt laeuft die Anwendung weiter, nur das Spiel steht (timeScale 0) und es wird mit 10 FPS
    // gezeichnet - spart genauso Akku und Waerme.
    internal sealed class BackgroundPause : MonoBehaviour
    {
        private bool bgPaused;
        private float bgTimeScale = 1f;
        private int bgFps, bgVsync;

        private void OnApplicationFocus(bool focus)
        {
            try
            {
                if (!focus && Plugin.PauseInBackground.Value && !bgPaused)
                {
                    bgPaused = true;
                    bgTimeScale = Time.timeScale; bgFps = Application.targetFrameRate; bgVsync = QualitySettings.vSyncCount;
                    if (Time.timeScale > 0f) Time.timeScale = 0f;
                    QualitySettings.vSyncCount = 0; Application.targetFrameRate = 10;
                }
                else if (focus && bgPaused)
                {
                    bgPaused = false;
                    if (Time.timeScale == 0f) Time.timeScale = bgTimeScale;
                    QualitySettings.vSyncCount = bgVsync; Application.targetFrameRate = bgFps;
                }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Background pause: " + e.Message); }
        }
    }
}
