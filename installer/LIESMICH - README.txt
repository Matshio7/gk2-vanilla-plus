GK2 VANILLA+  (Ultrawide, Performance & QoL)  {VERSION}
by McFly7 · https://github.com/Matshio7/gk2-vanilla-plus
=========================================================

[DEUTSCH]  (English below)

WAS IST DAS?
- Ultrawide: schaltet 21:9- und 32:9-Auflösungen im Grafikmenü frei
  (z. B. 2560x1080, 3440x1440, 3840x1080, 5120x1440).
- Tweaks: Mod-Menü im Spiel (F9) mit Einstellungen, die das Spiel sonst
  versteckt: einzelne Grafikoptionen, Bildraten-Begrenzung/VSync,
  Kamera-Zoom, Autosave, Logos überspringen, Pause im Hintergrund,
  FPS-Anzeige (F10) und mehr. Jede Option hat eine Erklärung, wenn du mit
  der Maus darüberfährst.
Es werden keine Spieldateien verändert. Spielstände bleiben unberührt.

INSTALLATION (Windows)
1. ZIP-Datei komplett entpacken (Rechtsklick > "Alle extrahieren").
2. Im entpackten Ordner "Installieren.bat" doppelklicken.
   Falls Windows warnt ("Der Computer wurde durch Windows geschützt"):
   "Weitere Informationen" > "Trotzdem ausführen".
3. Der Spielordner wird automatisch gefunden. Falls nicht: "Durchsuchen"
   und den Ordner wählen, in dem GraveyardKeeper2.exe liegt
   (Steam: Rechtsklick auf das Spiel > Verwalten > Lokale Dateien durchsuchen).
4. "Installieren" klicken. Danach das Spiel ganz normal über Steam starten.

IM SPIEL
- F9  = Mod-Menü        - F10 = FPS-Anzeige
- F6  = Wochenplan      - F7  = HUD ausblenden (Screenshots)
- Im Mod-Menü: "Jetzt speichern" speichert sofort (optional auch per Taste).
- Im Mod-Menü unter "FPS-Anzeige": Ecke und Inhalt wählen
  (FPS, 1%-Low, Frametime, CPU, GPU, RAM, VRAM, Auflösung, Uhrzeit).
- Ultrawide-Auflösung: Einstellungen > Grafik > Auflösung.

AKTUALISIEREN
Im Spiel zeigt das Mod-Menü (F9) neue Versionen an: "Speichern & aktualisieren".
Oder "Installieren.bat" starten > "Online nach Updates suchen".
Deine Einstellungen bleiben dabei erhalten.

DEINSTALLIEREN
"Installieren.bat" starten > "Deinstallieren". Auf Wunsch wird auch der
Mod-Loader (BepInEx) entfernt, dann ist das Spiel wieder im Originalzustand.

MAC (CrossOver) / LINUX (Proton)
Den Inhalt von "installer/files" in den Spielordner kopieren und für die
Flasche einmalig die DLL-Überschreibung "winhttp" auf "nativ, builtin"
stellen (CrossOver: Flasche > Wine-Konfiguration > Bibliotheken;
Proton: Startoption WINEDLLOVERRIDES="winhttp=n,b" %command%).
Im Mod-Menü gibt es dort zusätzlich den "Controller-Fix" gegen Ruckler mit
PlayStation-Controllern (wirkt nach Neustart von Spiel UND Steam).

PROBLEME?
Log-Datei: <Spielordner>/BepInEx/LogOutput.log – bitte mitschicken.


[ENGLISH]

WHAT IS THIS?
- Ultrawide: unlocks 21:9 and 32:9 resolutions in the graphics menu.
- Tweaks: in-game mod menu (F9) with settings the game hides: individual
  graphics options, frame rate cap/VSync, camera zoom, autosave, skip logos,
  pause in background, FPS display (F10) and more. Hover an option to see
  what it does.
No game files are modified. Save games are not touched.

INSTALL (Windows)
1. Extract the whole ZIP (right click > "Extract All").
2. Double-click "Installieren.bat" in the extracted folder.
   If Windows SmartScreen warns: "More info" > "Run anyway".
3. The game folder is detected automatically. If not, click "Browse" and pick
   the folder containing GraveyardKeeper2.exe
   (Steam: right click the game > Manage > Browse local files).
4. Click "Install", then start the game normally via Steam.

IN GAME
- F9 = mod menu, F10 = FPS display, F6 = week plan, F7 = hide HUD
- Mod menu: "Save now" saves immediately (optionally via a key).
- Mod menu > "FPS display": choose the corner and what to show
  (FPS, 1% low, frame time, CPU, GPU, RAM, VRAM, resolution, clock).
- Ultrawide resolution: Settings > Graphics > Resolution.

UPDATE
In game the mod menu (F9) shows new versions: "Save & update".
Or run "Installieren.bat" > "Check online for updates". Your settings are kept.

UNINSTALL
Run "Installieren.bat" > "Uninstall". Optionally removes the mod loader
(BepInEx) as well, restoring the original game.

MAC (CrossOver) / LINUX (Proton)
Copy the contents of "installer/files" into the game folder and set the DLL
override "winhttp" to "native, builtin" once (CrossOver: bottle > Wine
configuration > Libraries; Proton launch option:
WINEDLLOVERRIDES="winhttp=n,b" %command%). The mod menu then also shows a
"Controller fix" against stutter with PlayStation controllers (applies after
restarting the game AND Steam).

PROBLEMS?
Log file: <game folder>/BepInEx/LogOutput.log – please include it.

LIZENZ / LICENSE
Unverändert weitergeben (auch in kostenlosen Modpacks) ist erlaubt, mit
Namensnennung. Verändern oder Credits entfernen ist nicht erlaubt.
Free to redistribute unmodified (also in free mod packs) with credit.
Modifying it or removing the credits is not allowed. See docs/LICENSE.md.
Third-party licenses (BepInEx, Unity Doorstop): docs/licenses.
