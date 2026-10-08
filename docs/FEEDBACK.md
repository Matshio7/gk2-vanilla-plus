# Feedback-Übersicht (Steam-Workshop 3808053878)

Stand: 2026-10-08, gelesen aus den Steam-Diskussionen "Feature Requests & Wishes" (13 Beiträge), "Bug Reports" (15 von 17 Beiträgen lesbar, #16 und #17 nicht abrufbar; #17 ist Shadowblade) und dem Thread von Leika. Quelle der Angaben in Klammern.

Status: ✅ erledigt (Version) · 🔧 in 1.7.3 (Branch `release/1.7.3`), im Spiel getestet, noch nicht veröffentlicht · ❓ vermutlich behoben, Rückmeldung fehlt · ⬜ offen

## Feature-Wünsche

| Wer | Wunsch | Status |
|---|---|---|
| nice | Zombies umbenennen | ✅ 1.6.0 |
| nice | Handel: Menge für "Daumen hoch" automatisch einlegen | ✅ 1.6.0 (Regler + "Daumen-hoch-Waren einlegen", LT am Controller) |
| Captain Funsponge | Pins nur mit Inventar zählen (ohne Truhen) | ✅ 1.6.0 ("Inventory only") |
| Hush | Objekte verschieben / voller Rückbau beim Abbauen | ✅ 1.6.0 (optional, aus); Fabrik-Maschinen derzeit gesperrt (siehe Bugs) |
| K-Bro | Stadt-Aufträge anpinnen | ✅ 1.7.0 |
| K-Bro | Alchemie-Rezepte (Folio) anpinnen | ✅ 1.7.0 |
| LaSarrasine | Steam-Deck-Tearing | ✅ 1.6.0 |
| RubensPlayer13 | Portugiesisch (pt-BR) | ✅ 1.6.0 |
| chose20 | Übersetzungsvorlage immer im Ordner | ✅ 1.6.0 |
| Krunder | "Wake up" aus GK1 | ⬜ |
| medwedmem | Zombies auf der Karte (Marker/Symbole) | ⬜ |
| Saisuke | Zombies mit mehr Ausrüstungs-Slots | ⬜ |
| Stuchy | Zombie löschen | ⬜ |
| gl7 | Kleine Truhe direkt zur größeren aufwerten (Inhalt wandert mit) | ⬜ |
| gl7 | Controller: +10 bei Mengen bzw. Ziffern einzeln ändern | ⬜ |
| gl7 | Controller: Taschen mit Trigger/Bumper wechseln | ⬜ |
| HardWorkingLoner | Pin-Nadel kleiner (zu groß/verpixelt bei 4K) | 🔧 1.7.3 (30 % kleiner, weicher) |
| HardWorkingLoner | Pinnen per Rechtsklick statt Linksklick (versehentliche Pins) | 🔧 1.7.3 (Einstellung "Maustaste zum Anpinnen", Standard bleibt Links) |
| HardWorkingLoner | Pinnen im Tech-Tree | ⬜ (Roadmap 1.8) |
| jureth | Schnellerer Cursor beim Fabrik-Bau | ⬜ (aus Roadmap, Quelle nicht im Thread) |
| (Roadmap) | Unterstützer-Liste, Alternatives Hauptmenü, Fabrik-Maschinen verschiebbar | ⬜ |

Hinweis: Die Quelle von HardWorkingLoners Beitrag steht nicht in den gelesenen Threads (vermutlich ein neuerer Beitrag).

## Bugs

| Wer | Problem | Status |
|---|---|---|
| Leika (Linux) | Steinmetz-Fenster schließt sich nicht | ✅ 1.7.1 (Max-Knopf greift nicht mehr in die Tastenleiste) |
| aserraric | Nach Verschieben von Werkzeugkiste / Härte-Eimer lässt sich nichts mehr herstellen | ❓ 1.7.0 (Erweiterungen werden nach dem Verschieben wieder verbunden) |
| Dilma Rousseff | "Im Hintergrund pausieren" wirkt erst nach Aus-/Einschalten des Mods | ❓ 1.7.0 neu geschrieben (BackgroundPause), Rückmeldung fehlt |
| Xylem | Fabrik-Maschine verschieben: Ausgabe-Förderband verschwindet, Waren nicht abholbar | ⬜ derzeit nur gesperrt (1.7.2b); sauberes Neuverbinden Roadmap |
| piermaz38 | Manche Werkbänke: F ohne Wirkung (Log ohne Fehler) | 🔧/✅ vermutlich derselbe Hänger wie unten (1.7.2b) |
| Unlucky_Trefoil | Säge/Tisch nach Zombie-Nutzung hängt, auch ohne BepInEx; Max verschlimmert es | ✅ 1.7.2b (Max vorsichtig); bestehende Hänger: 🔧 Reparatur jetzt optional (Mod-Menü) |
| Drizz | Holzbank / Spinntisch hängt, nach Deinstallation Ultrawide noch aktiv ("alles deinstalliert?") | 🔧 Installer: Alles entfernen / Nur Vanilla+, Reste werden aufgelistet, Protokoll; Workshop-Abo kündigen entfernt nichts (Roadmap 1.8: Hinweis beim Start) |
| Shadowblade | Seit 1.7.2b: Warteschlange komplett weg, sobald eine Zutat ausgeht | 🔧 1.7.3 (Reparatur nur vorderster Auftrag, Standard aus; im Spiel getestet: Warteschlange bleibt) |
| John? | Autosaves im Kampf sind kaputt und verdrängen gute Backups | 🔧 1.7.3 (letzter Stand vor dem Kampf wird immer gesichert und nie verdrängt; Kampf-Backups markiert) |
| John? | Backup-Menü: rechte Knöpfe abgeschnitten/unleserlich | 🔧 1.7.3 (schmalere Beschriftung, Zeilenumbruch; im Spiel geprüft) |
| demch_5 | "12 Warnungen": Workshop-Loader (Fremd-Mod) hält Vanilla+ zur Freigabe zurück, `GK2 Tweaks` wird nicht geladen | kein Bug; Antwort-Entwurf liegt vor, Zeile für `GK2_WorkshopLoader.trust.txt` nennen. Einzelne Rest-Fehler stammen von "GK2 Framework Integration" anderer Mods |
| (Steam) | Hinweis "wegen Verstoß gegen die Community-Richtlinien entfernt" im Seitenquelltext | ✅ Fehlalarm: verstecktes Standard-Element (`display: none`) auf jeder Workshop-Seite; Steam-API: öffentlich, nicht gesperrt |

## Offene Fragen an die Nutzer
- Dilma Rousseff, aserraric: Tritt es mit 1.7.3 noch auf?
- Drizz: Hat die Deinstallationsanleitung geholfen? Log erbeten.
- piermaz38: Antwort auf die Rückfragen (verschobene Bänke, Max aus, Test ohne `winhttp.dll`).
