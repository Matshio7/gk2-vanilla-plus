#!/bin/zsh
# Baut das Release-Paket release/dist/GK2-VanillaPlus-<V>.zip (Installer + Updater + BepInEx + beide Mods).
# Voraussetzung: BepInEx 5.4.23.5 entpackt in _deps/BepInEx, Spiel-DLLs in _ref/Managed (siehe docs/BUILDING.md).
set -euo pipefail
ROOT="${0:A:h}"; B="$ROOT/_deps"; R="$ROOT/release"
V=$(sed -nE 's/.*PluginVersion = "([0-9.]+)".*/\1/p' "$ROOT/GK2Tweaks/Plugin.cs")
export PATH=/usr/local/share/dotnet:$PATH
(cd "$ROOT/GK2Ultrawide" && dotnet build -c Release -v q -nologo | grep -E "error|Build succeeded")
(cd "$ROOT/GK2Tweaks" && dotnet build -c Release -v q -nologo | grep -E "error|Build succeeded")
N="GK2-VanillaPlus-$V"; S="$R/stage/$N"; F="$S/installer/files"
rm -rf "$R/stage"; mkdir -p "$F/BepInEx/plugins/GK2Ultrawide" "$F/BepInEx/plugins/GK2Tweaks" "$F/BepInEx/GK2VanillaPlus" "$S/docs/licenses"
cp -R "$B/BepInEx/." "$F/"
cp "$ROOT/GK2Ultrawide/bin/Release/GK2Ultrawide.dll" "$F/BepInEx/plugins/GK2Ultrawide/"
cp "$ROOT/GK2Tweaks/bin/Release/GK2Tweaks.dll" "$F/BepInEx/plugins/GK2Tweaks/"
cp "$ROOT/LICENSE.md" "$F/BepInEx/GK2VanillaPlus/LICENSE.md"
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
cp "$ROOT/LICENSE.md" "$ROOT/THIRD-PARTY-NOTICES.txt" "$ROOT/docs/CHANGELOG.md" "$S/docs/"
cp "$B/BepInEx-LICENSE.txt" "$B/UnityDoorstop-LICENSE.txt" "$S/docs/licenses/"
mkdir -p "$R/dist"; rm -f "$R/dist/$N.zip"
(cd "$R/stage" && zip -qrX "$R/dist/$N.zip" "$N" -x '*.DS_Store')
# Steam-Workshop-Ordner: gleicher Inhalt wie das ZIP + Thumbnail.jpg (Vorschaubild fuer den Uploader des Spiels)
rm -rf "$R/workshop"; mkdir -p "$R/workshop"
cp -R "$S" "$R/workshop/GK2-VanillaPlus"
cp "$ROOT/workshop/Thumbnail.jpg" "$R/workshop/GK2-VanillaPlus/"
BOTTLE="$HOME/Library/Application Support/CrossOver/Bottles/Steam/drive_c"
if [ -d "$BOTTLE" ]; then rm -rf "$BOTTLE/GK2VanillaPlus-Workshop"; cp -R "$R/workshop/GK2-VanillaPlus" "$BOTTLE/GK2VanillaPlus-Workshop"; fi
rm -rf "$R/stage"
echo "$R/dist/$N.zip"
