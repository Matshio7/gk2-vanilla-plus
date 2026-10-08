# GK2 Vanilla+ – Projektkontext für Claude

Mod für Graveyard Keeper 2 (BepInEx 5 / HarmonyX, C#). Autor: Mats ("McFly7", GitHub Matshio7). Kommunikation auf Deutsch, kurz und direkt.

## Wo was liegt
- `GK2Tweaks/` – Haupt-Mod (Plugin.cs: Version, Configs, Patch-Liste; SafeMode.cs: Prüfung der Spiel-Member + Feature-Zuordnung).
- `GK2Ultrawide/` – kleiner Ultrawide-Plugin (1.0.0).
- `reference/src/` – dekompilierter Spielcode (zum Nachschlagen, nicht ändern).
- `lang/*.txt` – Übersetzungen `English => Übersetzung`; `python3 tools/extract_strings.py -v` muss überall "missing 0" melden.
- `docs/CHANGELOG.md` (EN+DE, wird eingebettet und als Steam-Change-Note genutzt), `docs/ROADMAP.md`.
- `workshop/description.bbcode` – Steam-Beschreibung (max 8000 Bytes inkl. CRLF).
- Spiel (CrossOver-Flasche "Steam"): `~/Library/Application Support/CrossOver/Bottles/Steam/drive_c/Program Files (x86)/Steam/steamapps/common/Graveyard Keeper 2`.

## Bauen & Testen
- Schnell prüfen: `cd GK2Tweaks && dotnet build -c Release --no-restore -nologo` (PATH: /usr/local/share/dotnet).
- Komplettes Paket: `./build.sh` (Release, Nexus-Zip, Workshop-Ordner `C:\GK2VanillaPlus-Workshop` in der Flasche).
- Konfigurationen: Release, Dev (Benchmark/Test-Touren, DevUpload), Nexus (ohne Update-Check).
- Im-Spiel-Test: `reference/crafttest.sh start|restore` (Dev-Build, sichert Spielstand + Config und legt sie zurück).
- Neue Patches: in die Patch-Liste in Plugin.cs, in SafeMode.FeatureOf eintragen, Member-Checks mit F()/M().

## Release-Ablauf
1. Version in Plugin.cs (`PluginVersion` nur Zahlen; Anzeige über `DisplayVersion`), CHANGELOG EN+DE.
2. `./build.sh`, Übersetzungen prüfen.
3. Steam: Uploader im Spiel (Shift+F11) oder Dev-Trigger `BepInEx/GK2VanillaPlus/upload.now`. Titel in `SteamWorkshop/workshop_items.json` muss "GK2 Vanilla+ – Ultrawide, Performance & Quality of Life" sein, sonst überschreibt der Uploader Titel/Beschreibung.
4. GitHub: Commit, Tag `vX.Y.Z`, `gh release create` mit Zip aus `release/dist/`.
5. Nexus (Mod 138): bestehende Datei "Update", alte archivieren, Version + Mod-Version setzen, Changelog.

## Regeln
- Pushen/Veröffentlichen nur nach Mats' Go.
- Keine Dateien endgültig löschen (z. B. Nexus-Archiv) – das macht Mats.
- Nie Passwörter eingeben oder für Mats einloggen.
- Interner Patch-Rhythmus (große Updates Montag, Hotfix Donnerstag) nur zur Planung, nie öffentlich nennen.
- Spielstände vor Tests sichern und danach unverändert zurücklegen.
