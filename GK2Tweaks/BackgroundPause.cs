using UnityEngine;

namespace GK2Tweaks
{
    // "Im Hintergrund pausieren": frueher ueber Application.runInBackground = false. Dabei verpasst Rewired aber
    // Geraete-Ereignisse und der Controller war nach Alt-Tab / Windows-Taste weg, bis man ihn neu einsteckt (gl7).
    // Jetzt laeuft die Anwendung weiter, nur das Spiel steht (timeScale 0) und es wird mit 10 FPS
    // gezeichnet - spart genauso Akku und Waerme.
    //
    // 1.7.6 (dilma): Pause griff unter Windows im Fenstermodus erst, nachdem man die Option einmal umgeschaltet hatte.
    // Das Umschalten setzt nur Application.runInBackground = true neu - vermutlich setzt das Spiel den Wert nach
    // Plugin.Awake zurueck oder ein Fokus-Ereignis geht verloren. Deshalb jetzt zusaetzlich in Update():
    //  - runInBackground wieder auf true, falls es zurueckgesetzt wurde (einmal geloggt)
    //  - Application.isFocused abfragen als Ersatz fuer verpasste/anfaengliche Fokus-Ereignisse
    //  - waehrend der Pause timeScale wieder auf 0, falls das Spiel es veraendert (einmal pro Pause geloggt)
    // Alle Uebergaenge werden geloggt, damit ein LogOutput.log die Ursache zeigt.
    internal sealed class BackgroundPause : MonoBehaviour
    {
        private const string Feature = "BackgroundPause";
        // so lange muss das Fenster laut isFocused ohne Fokus sein, bevor die Abfrage selbst pausiert
        // (kein Flackern beim Start oder beim kurzen Fensterwechsel)
        private const float PollDelay = 0.5f;
        private const int MaxPollErrors = 10;

        private bool bgPaused;
        private float bgTimeScale = 1f;
        private int bgFps, bgVsync;

        private bool ribLogged, scaleLogged, fpsLogged;
        private float unfocusedSince = -1f;
        private int pollErrors;
        private bool pollOff;

        private void OnApplicationFocus(bool focus)
        {
            try
            {
                Plugin.Log.LogInfo("Background pause: focus " + (focus ? "gained" : "lost") + " (event), option " + Plugin.PauseInBackground.Value
                    + ", runInBackground " + Application.runInBackground + ", timeScale " + Time.timeScale + ", targetFrameRate " + Application.targetFrameRate);
                if (!focus && Plugin.PauseInBackground.Value && !bgPaused) Enter("focus event");
                else if (focus && bgPaused) Leave("focus event");
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Background pause: " + e.Message); }
        }

        private void Update()
        {
            if (pollOff) return;
            try
            {
                // (1) Das Spiel (Einstellungen/Bildschirm anwenden) koennte runInBackground zurueckgesetzt haben.
                // Ohne runInBackground steht bei verlorenem Fokus die ganze Anwendung und Rewired verliert den Controller.
                if (!Application.runInBackground)
                {
                    if (!ribLogged) { Plugin.Log.LogWarning("Background pause: runInBackground was reset (by the game?) - set to true again"); ribLogged = true; }
                    Application.runInBackground = true;
                }

                // (2) Fokus abfragen - Ersatz fuer verpasste oder anfaengliche Fokus-Ereignisse
                bool focused = Application.isFocused;
                if (bgPaused)
                {
                    if (focused) { Leave("focus poll"); return; }
                    if (!Plugin.PauseInBackground.Value) { Leave("option turned off"); return; }

                    // (3) Spiel hat die Zeitskala waehrend der Pause veraendert -> wieder anhalten
                    if (Time.timeScale > 0f)
                    {
                        if (!scaleLogged) { Plugin.Log.LogWarning("Background pause: timeScale was changed during background pause (" + Time.timeScale + ") - set to 0 again"); scaleLogged = true; }
                        Time.timeScale = 0f;
                    }
                    // nur loggen, nicht dagegen ankaempfen (Bildraten-Einstellung von Vanilla+ oder Spiel)
                    if (Application.targetFrameRate != 10 && !fpsLogged)
                    {
                        Plugin.Log.LogInfo("Background pause: targetFrameRate changed during background pause to " + Application.targetFrameRate
                            + " (vSyncCount " + QualitySettings.vSyncCount + ")");
                        fpsLogged = true;
                    }
                    return;
                }

                if (focused || !Plugin.PauseInBackground.Value) { unfocusedSince = -1f; return; }
                // Unter Wine/CrossOver ist isFocused nicht verlaesslich - dort nur die Fokus-Ereignisse nutzen,
                // sonst koennte das Spiel dauerhaft angehalten bleiben
                if (WineFix.IsWine) return;
                float now = Time.realtimeSinceStartup;
                if (unfocusedSince < 0f) { unfocusedSince = now; return; }
                if (now - unfocusedSince >= PollDelay) Enter("focus poll (no focus event received)");
            }
            catch (System.Exception e)
            {
                SafeMode.Report(Feature, e);
                // wirft die Abfrage dauernd, bleibt es bei den Fokus-Ereignissen wie bis 1.7.5
                if (++pollErrors >= MaxPollErrors) { pollOff = true; Plugin.Log.LogWarning("Background pause: focus polling turned off after repeated errors"); }
            }
        }

        private void Enter(string reason)
        {
            bgPaused = true;
            scaleLogged = false; fpsLogged = false; unfocusedSince = -1f;
            bgTimeScale = Time.timeScale; bgFps = Application.targetFrameRate; bgVsync = QualitySettings.vSyncCount;
            if (Time.timeScale > 0f) Time.timeScale = 0f;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 10;
            Plugin.Log.LogInfo("Background pause: paused (" + reason + "), saved timeScale " + bgTimeScale + ", targetFrameRate " + bgFps + ", vSyncCount " + bgVsync);
        }

        private void Leave(string reason)
        {
            bgPaused = false;
            unfocusedSince = -1f;
            // Lief beim Pausieren gerade der Logo-Schnelldurchlauf (timeScale 50), nicht 50 zurueckholen -
            // Intro setzt dann nicht mehr zurueck, weil zwischendurch 0 galt
            float restore = bgTimeScale >= 10f ? 1f : bgTimeScale;
            if (Time.timeScale == 0f) Time.timeScale = restore;
            QualitySettings.vSyncCount = bgVsync; Application.targetFrameRate = bgFps;
            Plugin.Log.LogInfo("Background pause: resumed (" + reason + "), timeScale " + Time.timeScale + ", targetFrameRate " + Application.targetFrameRate + ", vSyncCount " + QualitySettings.vSyncCount);
        }
    }
}
