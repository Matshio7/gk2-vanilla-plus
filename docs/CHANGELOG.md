# Changelog

## 1.5.0 – 2026-09-27

**EN**
- New: **safe mode** – after a game update the mod checks everything it relies on. If something changed, only the affected feature is turned off (with a note in the mod menu) instead of causing errors – the rest keeps working. Features that keep throwing errors are turned off automatically too.
- Fix: the pinned list no longer covers the reputation window of the main NPCs (it moves below it)
- New: FPS display – **order of the values** can be changed with arrows in the mod menu, optional **separator** between the values (long dash, bar or dot)
- New: **controller support for the mod menu** – D-pad / left stick to select and change, A to confirm, B or Start to close, LB/RB to reorder the FPS display. While a mod window is open, the game ignores the controller.
- New: **one-click profiles** in the mod menu – "Steam Deck / battery", "Performance", "Quality" and "Game default"
- New: **HUD to the center** (ultrawide, optional) – HUD, area name, NPC window and the mod displays move into the 16:9 area in the middle
- New: pins – optional counting of **all chests on the map**, a **short message with sound** when a pinned item is ready, and **automatic unpinning** when you start crafting it

**DE**
- Neu: **Sicherer Modus** – nach einem Spiel-Update prüft der Mod alles, worauf er zugreift. Hat sich etwas geändert, wird nur die betroffene Funktion abgeschaltet (mit Hinweis im Mod-Menü), statt Fehler zu verursachen – der Rest läuft weiter. Funktionen, die wiederholt Fehler werfen, werden ebenfalls automatisch abgeschaltet.
- Fix: Die Pin-Liste verdeckt nicht mehr das Ansehen-Fenster der Haupt-NPCs (sie rückt darunter)
- Neu: FPS-Anzeige – **Reihenfolge der Werte** im Mod-Menü per Pfeil einstellbar, optional **Trennzeichen** zwischen den Werten (langer Strich, senkrechter Strich oder Punkt)
- Neu: **Controller-Bedienung des Mod-Menüs** – Steuerkreuz / linker Stick zum Auswählen und Ändern, A bestätigt, B oder Start schließt, LB/RB verschiebt die Werte der FPS-Anzeige. Solange ein Mod-Fenster offen ist, ignoriert das Spiel den Controller.
- Neu: **Ein-Klick-Profile** im Mod-Menü – „Steam Deck / Akku“, „Leistung“, „Qualität“ und „Spiel-Standard“
- Neu: **HUD zur Mitte** (Ultrawide, optional) – HUD, Gebietsname, NPC-Fenster und die Mod-Anzeigen rücken in den 16:9-Bereich in der Mitte
- Neu: Pins – optional **alle Truhen auf der Karte** mitzählen, **kurze Meldung mit Ton**, wenn ein Pin fertig ist, und **automatisch lösen**, sobald du das Rezept herstellst

## 1.4.3 – 2026-09-27

**EN**
- New: **full-width rain** – rain and snow now cover the whole screen on ultrawide monitors and when zoomed out (the game only fills a 16:9 area). Inspired by "GK2 Ultrawide Rain Fix" by Dry Bones (own implementation)
- OLED black is a bit darker (near-black areas are now fully black)
- Fix: quests can now be pinned with the controller (both sticks)
- Fix: blockages like the tunnel breakthrough can now be pinned

**DE**
- Neu: **Regen über die ganze Breite** – Regen und Schnee füllen jetzt auf Ultrawide-Monitoren und beim Herauszoomen den ganzen Bildschirm (das Spiel füllt nur einen 16:9-Bereich). Angeregt durch „GK2 Ultrawide Rain Fix“ von Dry Bones (eigene Umsetzung)
- OLED-Schwarz ist etwas dunkler (fast schwarze Bereiche sind jetzt ganz schwarz)
- Fix: Quests lassen sich jetzt auch mit dem Controller anpinnen (beide Sticks)
- Fix: Hindernisse wie der Durchbruch im Tunnel lassen sich jetzt anpinnen

## 1.4.2 – 2026-09-26

**EN**
- Fix: pins in the build menu (e.g. blueprints in the yard like the workbench) and for town buildings can now be clicked with the mouse
- New: **OLED black** (Graphics) – pure black instead of dark gray around the map, e.g. outside the church or at the level edge

**DE**
- Fix: Pins im Baumenü (z. B. Baupläne im Hof wie die Werkbank) und bei Stadtgebäuden lassen sich jetzt auch mit der Maus anklicken
- Neu: **OLED-Schwarz** (Grafik) – reines Schwarz statt Dunkelgrau um die Karte herum, z. B. außerhalb der Kirche oder am Levelrand

## 1.4.1 – 2026-09-26

**EN**
- Fix: the build menu (builder desk) threw an error and did not open correctly. Pins in the build menu and for town buildings now work without patching those windows.

**DE**
- Fix: Das Baumenü (Bauplan-Tisch) warf einen Fehler und öffnete nicht richtig. Pins im Baumenü und bei Stadtgebäuden funktionieren jetzt, ohne diese Fenster zu patchen.

## 1.4.0 – 2026-09-26

**EN**
- New: **pin recipes** – a small pin in the top right corner of recipes (workbenches), blueprints (build menu) and town buildings. Pinned items are listed at the screen edge with have/need per ingredient (your inventory, not chests), green when you have enough. Corner and size selectable. With a controller: press both sticks (L3 + R3) on a recipe.
- New: pin **quests** and **single crafts** (e.g. clearing blockages) – pin in the header of the recipe or quest window; quest pins show the current task
- New: pins are **kept per save**
- New: **save overview** in the mod menu in the main menu – day, date, graveyard/church quality, "Play" button and the backups of each save
- New: **camera** – zoom presets on F5, separate zoom indoors, smooth zoom with the mouse wheel, smooth transitions
- New: **screenshot key** (F12) in up to 4× resolution, optionally without HUD
- New: **readability** – size of the mod displays (80–200 %), high contrast mode, larger tooltips
- New: **languages** – French, Spanish, Russian and Chinese; more languages via a simple text file (BepInEx/GK2VanillaPlus/lang/_template.txt)
- New: **"Mods" button** in the main menu and the pause menu (Esc) – opens the mod menu, closing it returns to the game menu
- New: **graphics benchmark** in the mod menu – runs through all graphics tiers (plus your own settings), measures FPS without a limit and shows a score and a recommendation. Nothing is changed, results are also saved to BepInEx/GK2VanillaPlus/benchmark.txt
- New: **What's new** window – shown once after an update, and anytime via the button in the mod menu
- New: FPS display can show the **in-game weekday** and the **in-game time**
- Fix: the camera zoom no longer affects the main menu (always 100 % there)
- Fix: clicks in mod windows no longer reach game buttons underneath
- Pinning inspired by "Recipe Pin" by farfars (own implementation)

**DE**
- Neu: **Rezepte anpinnen** – eine kleine Pinnadel oben rechts an Rezepten (Werkbänke), Bauplänen (Baumenü) und Stadtgebäuden. Angepinntes steht als Liste am Bildschirmrand mit Haben/Brauchen je Zutat (dein Inventar, ohne Truhen), grün sobald genug da ist. Ecke und Größe einstellbar. Mit Controller: auf dem Rezept beide Sticks drücken (L3 + R3).
- Neu: **Quests** und **Einzel-Handwerk** (z. B. Hindernisse räumen) anpinnen – Pinnadel in der Überschrift des Rezept- oder Quest-Fensters; Quest-Pins zeigen die aktuelle Aufgabe
- Neu: Pins werden **pro Spielstand gespeichert**
- Neu: **Spielstand-Übersicht** im Mod-Menü im Hauptmenü – Tag, Datum, Friedhof-/Kirchen-Qualität, Button „Spielen“ und die Backups je Spielstand
- Neu: **Kamera** – Zoom-Stufen auf F5, eigener Zoom in Innenräumen, stufenloser Zoom mit dem Mausrad, weiche Übergänge
- Neu: **Screenshot-Taste** (F12) in bis zu 4-facher Auflösung, wahlweise ohne HUD
- Neu: **Lesbarkeit** – Größe der Mod-Anzeigen (80–200 %), Modus „Hoher Kontrast“, größere Tooltips
- Neu: **Sprachen** – Französisch, Spanisch, Russisch und Chinesisch; weitere Sprachen über eine einfache Textdatei (BepInEx/GK2VanillaPlus/lang/_template.txt)
- Neu: **Button „Mods“** im Hauptmenü und im Pausenmenü (Esc) – öffnet das Mod-Menü, beim Schließen geht es zurück ins Spielmenü
- Neu: **Grafik-Benchmark** im Mod-Menü – geht alle Grafikstufen durch (plus deine eigenen Einstellungen), misst die FPS ohne Limit und zeigt Score und Empfehlung. Es wird nichts verändert, das Ergebnis steht zusätzlich in BepInEx/GK2VanillaPlus/benchmark.txt
- Neu: Fenster **„Was ist neu?“** – erscheint einmal nach einem Update und jederzeit über den Button im Mod-Menü
- Neu: FPS-Anzeige kann den **Wochentag** und die **Uhrzeit im Spiel** zeigen
- Fix: Der Kamera-Zoom wirkt nicht mehr im Hauptmenü (dort immer 100 %)
- Fix: Klicks in Mod-Fenstern lösen keine Spiel-Buttons darunter mehr aus
- Anpinnen angeregt durch „Recipe Pin“ von farfars (eigene Umsetzung)

## 1.3.0 – 2026-09-26

**EN**
- New: **week plan** (F6, key configurable) – what is possible on which weekday, today and tomorrow highlighted, with the game's day icons. Spoiler-free: only features you have already unlocked.
- New: **daily reminder** – each morning a game notification shows what is possible today (shown three times as long as normal notifications) (can be turned off)
- New: **save backups** – before the game overwrites a save, the previous one is backed up (default: keep 5 per slot, at most every 10 min). Restore from the mod menu in the main menu. Stored in the game folder, not in Steam Cloud.
- New: **hide HUD** (F7) for clean screenshots, Esc shows it again
- Idea for the daily reminder inspired by "Daily Reminder" by MrsKiraSayers (own implementation)

**DE**
- Neu: **Wochenplan** (F6, Taste frei wählbar) – was an welchem Wochentag möglich ist, heute und morgen hervorgehoben, mit den Tagessymbolen des Spiels. Spoilerfrei: nur bereits Freigeschaltetes.
- Neu: **Tagesübersicht am Morgen** – jeden Morgen eine Spiel-Benachrichtigung, was heute geht (dreimal so lange sichtbar wie normale Meldungen) (abschaltbar)
- Neu: **Spielstand-Backups** – bevor das Spiel einen Stand überschreibt, wird der alte gesichert (Standard: 5 pro Spielstand, höchstens alle 10 min). Wiederherstellen im Mod-Menü im Hauptmenü. Liegen im Spielordner, nicht in der Steam Cloud.
- Neu: **HUD ausblenden** (F7) für saubere Screenshots, Esc blendet es wieder ein
- Idee zur Tagesübersicht angeregt durch „Daily Reminder“ von MrsKiraSayers (eigene Umsetzung)

## 1.2.0 – 2026-09-25 – GK2 Vanilla+

**EN**
- New name: **GK2 Vanilla+** (Ultrawide + Tweaks), first public release on GitHub
- Update check: the mod menu shows new versions; "Save & update" installs them in one click (Windows)
- Installer: "Check online for updates" button
- New license: free redistribution in mod packs, no modifications (see LICENSE.md)

**DE**
- Neuer Name: **GK2 Vanilla+** (Ultrawide + Tweaks), erste öffentliche Version auf GitHub
- Update-Prüfung: Das Mod-Menü zeigt neue Versionen an, „Speichern & aktualisieren“ installiert sie mit einem Klick (Windows)
- Installer: Button „Online nach Updates suchen“
- Neue Lizenz: Weitergabe in Modpacks erlaubt, keine Veränderungen (siehe LICENSE.md)

## 1.1.0 – 2026-09-25 (Ultrawide + Tweaks bundle, private test)

**EN**
- GK2 Ultrawide and GK2 Tweaks now ship as one package with a Windows installer (Installieren.bat)
- New: GK2 Tweaks – in-game mod menu (F9) in the game's own look, FPS display (F10)
- Individual graphics overrides (shadows, HBAO, lights, water, clouds, render mode), frame rate cap/VSync, physics rate
- Comfort: camera zoom, extra autosave, pause in background, skip logos, widened main menu on ultrawide, "modded" note
- Mod menu in German and English (follows the game language)
- Mac/Linux: optional controller fix against stutter with PlayStation controllers
- Manual save: "Save now" button in the mod menu, optional save key
- FPS display: choose the corner and what to show (FPS, 1% low, frame time, CPU, GPU, RAM, VRAM, resolution, clock)

**DE**
- GK2 Ultrawide und GK2 Tweaks kommen jetzt als ein Paket mit Windows-Installer (Installieren.bat)
- Neu: GK2 Tweaks – Mod-Menü im Spiel (F9) in der Optik des Spiels, FPS-Anzeige (F10)
- Einzelne Grafik-Einstellungen (Schatten, HBAO, Lichter, Wasser, Wolken, Render-Modus), Bildraten-Limit/VSync, Physik-Takt
- Komfort: Kamera-Zoom, zusätzlicher Autosave, Pause im Hintergrund, Logos überspringen, verbreitertes Hauptmenü auf Ultrawide, Hinweis „modded“
- Mod-Menü auf Deutsch und Englisch (folgt der Spielsprache)
- Mac/Linux: optionaler Controller-Fix gegen Ruckler mit PlayStation-Controllern
- Manuelles Speichern: Button „Jetzt speichern“ im Mod-Menü, optional eigene Taste
- FPS-Anzeige: Ecke und Inhalt wählbar (FPS, 1%-Low, Frametime, CPU, GPU, RAM, VRAM, Auflösung, Uhrzeit)

## 1.0.0 – 2026-09-23

**EN**
- Initial release
- Unlocks resolutions with an aspect ratio above 2:1 and widths above 5120 px
- `CustomResolution` config option for extra resolutions
- `MainMenuScaleX2` config option (Auto/On/Off)
- Keeps the game's built-in fallback resolution list if the monitor reports no resolutions (e.g. under Wine)

**DE**
- Erste Veröffentlichung
- Schaltet Auflösungen mit Seitenverhältnis über 2:1 und über 5120 px Breite frei
- Config-Option `CustomResolution` für zusätzliche Auflösungen
- Config-Option `MainMenuScaleX2` (Auto/On/Off)
- Behält die eingebaute Ersatzliste des Spiels, falls der Monitor keine Auflösungen meldet (z. B. unter Wine)
