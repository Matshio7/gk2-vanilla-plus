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
5. Beta (Vorabversion, z. B. 1.8.0b): in Plugin.cs `BetaNumber = N` und `BetaTag = " Beta N"` setzen (Stabil: 0 / ""), `PluginVersion` = kommende Zahl. GitHub-Tag `vX.Y.Z-beta.N`, `gh release create --prerelease` mit derselben Zip. Kein Steam/Nexus-Upload. Installer/Updater/Mod-Menü holen sie nur im Kanal "Beta"; "Stabil" nutzt `releases/latest` (Pre-releases zählen dort nie).
6. Nexus (Mod 138): bestehende Datei "Update", alte archivieren, Version + Mod-Version setzen, Changelog.

## Technische Stolpersteine
- ImageConversion nur per Reflection (direkte Referenz -> netstandard-2.1-Compilefehler).
- `OnRenderImage`: `dst` kann null sein -> Größen aus `src` nehmen; Komponente prüft selbst, ob sie noch aktiv sein darf.
- `Graphics.DrawTexture` (GUI-Shader) verdoppelt Farbe und Alpha: 0.5 = 1x.
- Kleine Steamworks-Methoden werden vom Mono-JIT inline eingebaut -> Patches greifen nicht, deshalb Transpiler (WorkshopUpload.cs).
- Labels.cs: drei Dictionaries (Names, TipsDe, Values) – keine doppelten Schlüssel.
- Spieler-Werkbänke: Aufträge mit mehr Durchgängen als Zutaten bleiben als "wartet" hängen (CraftRepair.cs).
- `CraftInteractionHandler.assignedCraftComponent` wird erst in `HasInteraction` gesetzt.
- Shell-Befehle auf dem Mac: einzelne Aufrufe kurz halten (lange Läufe in Hintergrund + Log, z. B. `(nohup ./build.sh > /tmp/gk2build.log 2>&1 &)`).

## Community & Links
- Steam Workshop 3808053878 (App 4358690), Titel muss mit "GK2 Vanilla+" beginnen.
- Nexus: Mod 138, Edit: https://www.nexusmods.com/games/graveyardkeeper2/mods/138/edit/files
- GitHub: Matshio7/gk2-vanilla-plus, Ko-fi: ko-fi.com/mcfly7
- Fehlermeldungen: Steam-Diskussion "Bug Reports", immer `BepInEx\LogOutput.log` erbitten.
- Offene Aufgaben: docs/ROADMAP.md.

## Slash-Befehle (.claude/commands)
- `/build` – bauen + Übersetzungen prüfen
- `/ingame-test` – Änderung automatisch im Spiel testen (Spielstand wird gesichert/zurückgelegt)
- `/release X.Y.Z` – Release vorbereiten, stoppt vor dem Veröffentlichen

## Regeln
- Pushen/Veröffentlichen nur nach Mats' Go.
- Keine Dateien endgültig löschen (z. B. Nexus-Archiv) – das macht Mats.
- Nie Passwörter eingeben oder für Mats einloggen.
- Interne Planungsnotizen stehen in CLAUDE.local.md (nur lokal, nicht im Repo) – nie öffentlich nennen.
- Spielstände vor Tests sichern und danach unverändert zurücklegen.
