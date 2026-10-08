# Roadmap – nächstes großes Update (1.8)

Stand: 2026-10-08. Ziel: Deinstallieren zuverlässig, Spielstände sicher.

## 1. Deinstallieren

Gefundene Schwachstellen:
- Workshop-Abo kündigen entfernt nichts aus dem Spielordner (McBinary, Drizz).
- Installer-Frage Ja/Nein missverständlich ("Nein" = BepInEx bleibt).
- Kein Nachprüfen: gesperrte/schreibgeschützte Dateien -> Abbruch, Reste bleiben.

Umsetzung:
- [ ] Mod-Menü (F9): "Deinstallieren" – sofort `doorstop_config.ini` `enabled = false` (lädt beim nächsten Start nichts mehr), nach dem Beenden räumt ein kleines Skript die Dateien weg (Windows, Wine/Proton, Mac/CrossOver).
- [ ] Start-Hinweis, wenn das Workshop-Abo weg ist (SteamUGC.GetItemState 3808053878): "Vanilla+ komplett entfernen?"
- [ ] Installer: Knöpfe "Alles entfernen (empfohlen)" / "Nur Mods entfernen", Schreibschutz aufheben, danach prüfen und Reste anzeigen, `uninstall.log`.
- [ ] Backups/Screenshots weiter nach Dokumente\GK2 Vanilla+ retten.

## 2. Spielstand-Sicherheit

- [ ] Spielstand-Check beim Laden + Knopf "Spielstand prüfen": hängende Aufträge (CraftRepair für alle Werkbänke), Erweiterungs-Links auf fehlende Objekte, Fabrik-Maschinen ohne Ausgabe-Band. Reparieren, Hinweis, Log.
- [ ] Automatisches Backup beim ersten Laden nach einem Mod-Update, als "behalten" markiert.
- [ ] Vor dem Deinstallieren einmal den Check laufen lassen.
- [ ] Prüfen + dokumentieren: welche Funktion schreibt was in den Spielstand (Verschieben, Sofort-Abbau/Erstattung, Zurückerstatten, Zombie-Namen, Max). Mod-eigene Daten (Pins, Wochenplan, Einstellungen, Backups) liegen außerhalb.
- [ ] Backups im Kampf (John?, Bug-Thread #12): Autosaves mitten im Kampf sind kaputt und verdrängen die guten. Backups während eines Kampfes nicht zählen/nicht rotieren, letztes Backup vor dem Kampf immer behalten.
- [ ] Automatischer Test (Dev-Tour wie crafttest): Spielstand laden -> Verschieben, Abbauen, Max, Zurückerstatten -> Test-Slot speichern -> Spiel OHNE Mod starten (doorstop aus) -> Test-Slot laden -> Player.log auf Fehler prüfen.

## 2b. Offen aus 1.7.2 (Bugs/Feedback)

- [ ] Hängende Werkbänke: sauberes Erkennen statt Opt-in-Reparatur (Spielcode prüfen: warum reagiert F nicht?). 1.7.2b entfernte versehentlich ganze Warteschlangen (Steam-Bericht Shadowblade), seit 1.7.2 final nur noch optional und nur der vorderste Auftrag.
- [ ] Pins im Tech-Tree (Forschung/Talente) – Feedback HardWorkingLoner.
- [ ] Pin-Nadel mit höher aufgelöster Grafik (jetzt kleiner + weich).
- [ ] Beta-Kanal: nach erstem Beta-Release (1.8.0b) Installer/Updater unter Windows testen.

## 3. Danach

- [ ] Fabrik-Maschinen wieder verschiebbar (Förderbänder richtig neu verbinden, gleicher Test).
- [ ] Alternatives Hauptmenü (Entwurf B, Weiterspielen-Karte).
- [ ] Wünsche: Truhe aufwerten mit Inhalt, LB/RB +10 / Taschen wechseln (gl7), schnellerer Cursor beim Fabrik-Bau (jureth), Zombies auf der Karte, Zombie löschen, "Wake up" aus GK1 (Krunder), Unterstützer-Liste.
- [ ] Store-Bilder für 1.7 (Auftrags-Pin, Alchemie-Pin, Max, Zurückerstatten).
