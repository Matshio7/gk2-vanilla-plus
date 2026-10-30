#!/bin/zsh
# Baut das Release-Paket release/dist/GK2-VanillaPlus-<V>.zip (Installer + Updater + BepInEx + beide Mods).
# Voraussetzung: BepInEx 5.4.23.5 entpackt in _deps/BepInEx, Spiel-DLLs in _ref/Managed (siehe docs/BUILDING.md).
set -euo pipefail
ROOT="${0:A:h}"; B="$ROOT/_deps"; R="$ROOT/release"
V=$(sed -nE 's/.*PluginVersion = "([0-9.]+)".*/\1/p' "$ROOT/GK2Tweaks/Plugin.cs")
export PATH=/usr/local/share/dotnet:$PATH
(cd "$ROOT/GK2Ultrawide" && dotnet build -c Release --no-restore -v q -nologo | grep -E "error|Build succeeded")
(cd "$ROOT/GK2Tweaks" && dotnet build -c Release --no-restore -v q -nologo -p:Version=$V | grep -E "error|Build succeeded")
# Optionale Bruecke zu "GK2 Mod Framework" (nur Kompilier-Referenz: reference/fw = Framework-Quellcode, MIT, nicht mitgeliefert)
[ -d "$ROOT/reference/fw" ] || git clone -q --depth 1 https://github.com/AcTePuKc/GK2-Mod-Framework.git "$ROOT/reference/fw"
(cd "$ROOT/FrameworkRef" && dotnet build -c Release -v q -nologo --source "$HOME/.nuget/packages" | grep -E "error|Build succeeded")
(cd "$ROOT/FrameworkBridge" && dotnet build -c Release -v q -nologo --source "$HOME/.nuget/packages" | grep -E "error|Build succeeded")
N="GK2-VanillaPlus-$V"; S="$R/stage/$N"; F="$S/installer/files"; L="$S/installer/loader"
rm -rf "$R/stage"; mkdir -p "$F/BepInEx/plugins/GK2Ultrawide" "$F/BepInEx/plugins/GK2Tweaks" "$F/BepInEx/GK2VanillaPlus" "$L" "$S/docs/licenses"
# Mod-Loader (BepInEx 5.4.23.5 + Doorstop) getrennt in installer/loader - NICHT in installer/files:
# Der Updater von 1.7.4 und aelter sucht einen Ordner "files" mit winhttp.dll und kopiert ihn komplett mit -Force ins
# Spiel - das wuerde das BepInEx des Spiels (ab 1.011) ueberschreiben. So bricht er mit "unerwartetes Format" ab.
# install.ps1/update.ps1 ab 1.7.5 nehmen installer/loader nur, wenn im Spielordner noch kein Loader liegt.
cp -R "$B/BepInEx/." "$L/"
cp "$ROOT/GK2Ultrawide/bin/Release/GK2Ultrawide.dll" "$F/BepInEx/plugins/GK2Ultrawide/"
cp "$ROOT/GK2Tweaks/bin/Release/GK2Tweaks.dll" "$F/BepInEx/plugins/GK2Tweaks/"
cp "$ROOT/LICENSE.md" "$F/BepInEx/GK2VanillaPlus/LICENSE.md"
# Sprachen: eingebaut in der DLL; die Vorlage liegt fuer eigene Uebersetzungen bei
python3 "$ROOT/tools/extract_strings.py" >/dev/null
# Hauptmenue-Hintergruende (eigene Dateien statt im DLL: Workshop laedt sie nur bei Aenderung neu)
mkdir -p "$F/BepInEx/GK2VanillaPlus/MenuBackground"; cp "$ROOT"/docs/menubg/bg?.jpg "$F/BepInEx/GK2VanillaPlus/MenuBackground/"
mkdir -p "$F/BepInEx/GK2VanillaPlus/lang"; cp "$ROOT/lang/_template.txt" "$F/BepInEx/GK2VanillaPlus/lang/translation-template.txt"
# Textdateien fuer Windows: UTF-8 mit BOM, CRLF, Versionsnummer eintragen
winText() { python3 - "$1" "$2" "$V" <<'PY'
import re, sys
src, dst, v = sys.argv[1:4]
s = open(src, encoding='utf-8-sig').read().replace('\r\n', '\n')
s = re.sub(r"\$Version = '[^']*'", "$Version = '" + v + "'", s, count=1).replace('{VERSION}', v)
open(dst, 'w', encoding='utf-8-sig', newline='').write(s.replace('\n', '\r\n'))
PY
}
winText "$ROOT/installer/install.ps1" "$S/installer/install.ps1"
winText "$ROOT/installer/update.ps1" "$F/BepInEx/GK2VanillaPlus/update.ps1"
winText "$ROOT/installer/LIESMICH - README.txt" "$S/LIESMICH - README.txt"
cp "$ROOT/installer/Installieren.bat" "$S/"
# Logo und Fenstersymbol fuer das Installationsfenster (fehlen sie, zeigt der Installer nur Text)
mkdir -p "$S/installer/assets"
cp "$ROOT/docs/logo/GK2-VanillaPlus-Banner.png" "$S/installer/assets/banner.png"
cp "$ROOT/docs/logo/GK2-VanillaPlus-Icon.png" "$S/installer/assets/icon.png"
cp "$ROOT/LICENSE.md" "$ROOT/THIRD-PARTY-NOTICES.txt" "$ROOT/docs/CHANGELOG.md" "$S/docs/"
cp "$B/BepInEx-LICENSE.txt" "$B/UnityDoorstop-LICENSE.txt" "$S/docs/licenses/"
mkdir -p "$R/dist"; rm -f "$R/dist/$N.zip"
(cd "$R/stage" && zip -qrX "$R/dist/$N.zip" "$N" -x '*.DS_Store')
# Steam-Workshop-Ordner (= Inhalt, den der Uploader hochlaedt): NUR BepInEx/plugins/GK2Tweaks/** und BepInEx/plugins/GK2Ultrawide/**.
# Seit Spiel-Update 1.011 bringt das Spiel BepInEx selbst mit, und "GK2.WorkshopSync" kopiert abonnierte Items eigenstaendig
# ins Spielverzeichnis (und entfernt die Dateien beim Abschalten im Mods-Menue wieder). Deshalb im Item:
#  - keine Loader-Dateien (wuerden den Loader des Spiels bei JEDEM Abonnenten ueberschreiben),
#  - nichts im Wurzelordner ausser BepInEx/ (Installieren.bat, LICENSE.md, Thumbnail.jpg ... landeten sonst im Spielordner),
#  - nichts in BepInEx/GK2VanillaPlus: dort liegen Nutzerdaten (Backups, Screenshots, pins.txt, startup.pending, mine.png).
# Menue-Hintergruende, Lizenz und Changelog liegen im Plugin-Ordner (MenuBackground.cs/WorkshopUpload.cs lesen sie dort);
# die Uebersetzungsvorlage schreibt der Mod selbst aus der DLL. Kein update.ps1: Updates kommen ueber den Workshop.
# Aeltere Spielversionen (ohne eigenes BepInEx): GitHub-ZIP mit Installer (installer/loader).
W="$R/workshop/GK2-VanillaPlus"; WT="$W/BepInEx/plugins/GK2Tweaks"
rm -rf "$R/workshop"; mkdir -p "$WT/MenuBackground" "$W/BepInEx/plugins/GK2Ultrawide"
cp "$F/BepInEx/plugins/GK2Tweaks/GK2Tweaks.dll" "$WT/"
cp "$F/BepInEx/plugins/GK2Ultrawide/GK2Ultrawide.dll" "$W/BepInEx/plugins/GK2Ultrawide/"
cp "$F"/BepInEx/GK2VanillaPlus/MenuBackground/bg?.jpg "$WT/MenuBackground/"
cp "$ROOT/LICENSE.md" "$ROOT/THIRD-PARTY-NOTICES.txt" "$ROOT/docs/CHANGELOG.md" "$WT/"
# Vorschaubild ausserhalb des hochgeladenen Ordners (WorkshopUpload setzt es beim Autor aus BepInEx/GK2VanillaPlus/workshop_thumbnail.jpg)
cp "$ROOT/workshop/Thumbnail.jpg" "$R/workshop/Thumbnail.jpg"
# Sicherheitspruefung: im Wurzelordner nur BepInEx/, darunter nur plugins/GK2Tweaks und plugins/GK2Ultrawide,
# keine Loader-Dateien, genau unsere zwei DLLs, Pflichtdateien vorhanden - sonst Abbruch
ROOTS=$(cd "$W" && find . -mindepth 1 -maxdepth 1 | sort | tr '\n' ' ')
UNDER=$(cd "$W" && find ./BepInEx -mindepth 1 -maxdepth 2 | sort | tr '\n' ' ')
BAD=$(cd "$W" && find . \( -iname 'winhttp.dll' -o -iname 'doorstop_config.ini' -o -iname '.doorstop_version' -o -iname 'doorstop*' \
  -o -iname '*.ps1' -o -iname '*.bat' -o -iname '0Harmony*.dll' -o -iname 'BepInEx*.dll' -o -iname 'Mono.Cecil*.dll' -o -iname 'MonoMod*.dll' \) -print)
DLLS=$(cd "$W" && find . -iname '*.dll' | sort | tr '\n' ' ')
if [ "$ROOTS" != "./BepInEx " ] || [ "$UNDER" != "./BepInEx/plugins ./BepInEx/plugins/GK2Tweaks ./BepInEx/plugins/GK2Ultrawide " ] || [ -n "$BAD" ] \
   || [ "$DLLS" != "./BepInEx/plugins/GK2Tweaks/GK2Tweaks.dll ./BepInEx/plugins/GK2Ultrawide/GK2Ultrawide.dll " ]; then
  echo "FEHLER: Workshop-Ordner enthaelt mehr als BepInEx/plugins/GK2Tweaks + GK2Ultrawide oder Loader-/Fremddateien - Abbruch:" >&2
  echo "Wurzel: $ROOTS" >&2; echo "BepInEx: $UNDER" >&2
  [ -n "$BAD" ] && echo "$BAD" >&2
  echo "DLLs: $DLLS" >&2
  exit 1
fi
for f in BepInEx/plugins/GK2Tweaks/GK2Tweaks.dll BepInEx/plugins/GK2Ultrawide/GK2Ultrawide.dll BepInEx/plugins/GK2Tweaks/LICENSE.md \
         BepInEx/plugins/GK2Tweaks/THIRD-PARTY-NOTICES.txt BepInEx/plugins/GK2Tweaks/CHANGELOG.md BepInEx/plugins/GK2Tweaks/MenuBackground/bg1.jpg; do
  [ -f "$W/$f" ] || { echo "FEHLER: Workshop-Ordner ohne $f - Abbruch" >&2; exit 1; }
done
# GitHub-ZIP: installer/files darf keine Loader-Dateien enthalten (siehe oben, alter Updater)
if [ -n "$(cd "$F" && find . \( -iname 'winhttp.dll' -o -iname 'doorstop*' -o -iname '.doorstop_version' -o -ipath './BepInEx/core' \) -print)" ]; then
  echo "FEHLER: installer/files enthaelt Loader-Dateien - Abbruch" >&2; exit 1
fi
[ -f "$L/winhttp.dll" ] || { echo "FEHLER: installer/loader ohne winhttp.dll - Abbruch" >&2; exit 1; }
BOTTLE="$HOME/Library/Application Support/CrossOver/Bottles/Steam/drive_c"
GAME="$BOTTLE/Program Files (x86)/Steam/steamapps/common/Graveyard Keeper 2"
if [ -d "$GAME/BepInEx" ]; then
  mkdir -p "$GAME/BepInEx/GK2VanillaPlus"
  cp "$ROOT/workshop/description.bbcode" "$GAME/BepInEx/GK2VanillaPlus/workshop_description.bbcode"
  cp "$ROOT/workshop/Thumbnail.jpg" "$GAME/BepInEx/GK2VanillaPlus/workshop_thumbnail.jpg"
fi
if [ -d "$BOTTLE" ]; then rm -rf "$BOTTLE/GK2VanillaPlus-Workshop"; cp -R "$W" "$BOTTLE/GK2VanillaPlus-Workshop"; fi
# Nexus-Ausgabe: ohne Update-Pruefung und Online-Updater (Nexus erlaubt keine Selbst-Updates)
(cd "$ROOT/GK2Tweaks" && dotnet build -c Nexus --no-restore -v q -nologo -p:Version=$V | grep -E "error|Build succeeded")
NX="$R/stage/nexus/$N"; rm -rf "$R/stage/nexus"; mkdir -p "$R/stage/nexus"; cp -R "$S" "$NX"
cp "$ROOT/GK2Tweaks/bin/Nexus/GK2Tweaks.dll" "$NX/installer/files/BepInEx/plugins/GK2Tweaks/"
rm -f "$NX/installer/files/BepInEx/GK2VanillaPlus/update.ps1"
# Nexus: Menue-Hintergruende auf 3840 px verkleinern (Upload-Datei bleibt unter 10 MB)
for j in "$NX"/installer/files/BepInEx/GK2VanillaPlus/MenuBackground/bg?.jpg; do sips -Z 3840 -s format jpeg -s formatOptions 80 "$j" --out "$j" >/dev/null; done
python3 - "$NX" <<'PY'
import re, sys
nx = sys.argv[1]
p = nx + '/installer/install.ps1'
s = open(p, encoding='utf-8-sig').read().replace('$OnlineUpdate = $true', '$OnlineUpdate = $false', 1)
s = re.sub(r'\$online    = New-Btn[^\n]*\n\$online\.SetBounds[^\n]*\n', '', s)
s = re.sub(r'\$online\.Add_Click\(\{.*?\n\}\)\r?\n', '', s, count=1, flags=re.S)
s = re.sub(r'if \(\$OnlineUpdate\)[^\n]*\n', '', s)
open(p, 'w', encoding='utf-8-sig', newline='').write(s)
p = nx + '/LIESMICH - README.txt'
s = open(p, encoding='utf-8-sig', newline='').read()
s = re.sub(r'AKTUALISIEREN\r\n.*?\r\n\r\n', 'AKTUALISIEREN\r\nNeue Version auf Nexus Mods laden, entpacken und "Installieren.bat" > "Aktualisieren".\r\nDeine Einstellungen bleiben dabei erhalten.\r\n\r\n', s, count=1, flags=re.S)
s = re.sub(r'UPDATE\r\n.*?\r\n\r\n', 'UPDATE\r\nDownload the new version from Nexus Mods, extract it and run "Installieren.bat" > "Update".\r\nYour settings are kept.\r\n\r\n', s, count=1, flags=re.S)
open(p, 'w', encoding='utf-8-sig', newline='').write(s)
PY
# nur den alten Build derselben Version ersetzen - aeltere Nexus-Archive bleiben liegen
mkdir -p "$R/nexus"; rm -f "$R/nexus/$N-Nexus.zip"
# Nexus: NUR die Dateien zum Entpacken, keine .bat/.ps1 - Skripte im Archiv fuehren zur Quarantaene ("Executables").
# Ohne Mod-Loader (der liegt in installer/loader): "in den Spielordner entpacken" darf das BepInEx des Spiels (ab 1.011)
# nicht ueberschreiben. Fuer aeltere Spielversionen muss BepInEx 5.4.23.5 vorher installiert sein (Nexus-Beschreibung).
(cd "$NX/installer/files" && zip -qrX "$R/nexus/$N-Nexus.zip" . -x '*.DS_Store')
rm -rf "$R/stage"
echo "$R/dist/$N.zip"
ls -la "$R/nexus"
