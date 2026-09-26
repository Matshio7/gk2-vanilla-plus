# Building from source / Aus dem Quellcode bauen

Building is allowed for personal use and to verify what the mod does. Publishing modified builds is not allowed, see [LICENSE.md](../LICENSE.md).
*Kompilieren ist für den privaten Gebrauch und zur Überprüfung erlaubt. Veränderte Builds zu veröffentlichen ist nicht erlaubt.*

Requirements / Voraussetzungen: .NET SDK 6+ (any OS), Graveyard Keeper 2, BepInEx 5.4.23.5.

The game's assemblies are **not** part of this repository. Point the build at your own game install:
*Die Spiel-DLLs sind nicht im Repository. Beim Bauen auf die eigene Installation verweisen:*

```sh
cd GK2Tweaks   # or GK2Ultrawide
dotnet build -c Release \
  -p:ManagedDir="<GameFolder>/GraveyardKeeper2_Data/Managed/" \
  -p:BepInExDir="<GameFolder>/BepInEx/core/"
```

Output: `bin/Release/GK2Tweaks.dll` / `GK2Ultrawide.dll` → `<GameFolder>/BepInEx/plugins/<Name>/`.

`-c Dev` additionally builds the author's measuring tools (benchmark, profiler, UI export).

`release/build_bundle.sh` builds the release ZIP (expects BepInEx 5.4.23.5 unpacked in `_deps/BepInEx`).

## How it works / Funktionsweise

- **Ultrawide:** `ResolutionConfig.InitAvailableResolutions()` skips every resolution with `width / height > 2` or `width > 5120`. A Harmony postfix re-adds them via the game's own `TryAddAvailableResolution()`.
- **Graphics:** a postfix on `GraphicsTierConfig.ApplyTier` overrides single fields of the game's own `PlatformFeatures` (the same switches the console versions use).
- **Main menu:** an `OnRenderImage` pass on the world camera fills the side bars with a blurred, darkened copy of the menu image.
- Nothing is written to game files. Settings live in `BepInEx/config/mats.gk2.tweaks.cfg`.

## Translations

The mod menu texts are written in German and English in the code. All other languages come from `lang/<code>.txt`
(key = English text, `English => translation`). The files are embedded into the DLL at build time.
`python3 tools/extract_strings.py -v` rewrites `lang/_template.txt` and lists missing keys per language.
Players can add or override a language by putting `<code>.txt` into `<game>/BepInEx/GK2VanillaPlus/lang/`.
