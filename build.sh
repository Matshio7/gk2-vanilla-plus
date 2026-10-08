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
N="GK2-VanillaPlus-$V"; S="$R/stage/$N"; F="$S/installer/files"
rm -rf "$R/stage"; mkdir -p "$F/BepInEx/plugins/GK2Ultrawide" "$F/BepInEx/plugins/GK2Tweaks" "$F/BepInEx/GK2VanillaPlus" "$S/docs/licenses"
cp -R "$B/BepInEx/." "$F/"
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
# Steam-Workshop-Ordner: gleicher Inhalt wie das ZIP + Thumbnail.jpg (Vorschaubild fuer den Uploader des Spiels)
rm -rf "$R/workshop"; mkdir -p "$R/workshop"
cp -R "$S" "$R/workshop/GK2-VanillaPlus"
cp "$ROOT/workshop/Thumbnail.jpg" "$R/workshop/GK2-VanillaPlus/"
BOTTLE="$HOME/Library/Application Support/CrossOver/Bottles/Steam/drive_c"
GAME="$BOTTLE/Program Files (x86)/Steam/steamapps/common/Graveyard Keeper 2"
if [ -d "$GAME/BepInEx" ]; then mkdir -p "$GAME/BepInEx/GK2VanillaPlus"; cp "$ROOT/workshop/description.bbcode" "$GAME/BepInEx/GK2VanillaPlus/workshop_description.bbcode"; fi
if [ -d "$BOTTLE" ]; then rm -rf "$BOTTLE/GK2VanillaPlus-Workshop"; cp -R "$R/workshop/GK2-VanillaPlus" "$BOTTLE/GK2VanillaPlus-Workshop"; fi
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
mkdir -p "$R/nexus"; rm -f "$R/nexus/"*.zip
# Nexus: NUR die Dateien zum Entpacken, keine .bat/.ps1 - Skripte im Archiv fuehren zur Quarantaene ("Executables")
(cd "$NX/installer/files" && zip -qrX "$R/nexus/$N-Nexus.zip" . -x '*.DS_Store')
rm -rf "$R/stage"
echo "$R/dist/$N.zip"
ls -la "$R/nexus"
