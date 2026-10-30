# GK2 Vanilla+ (Ultrawide + Tweaks) by McFly7 - Installer (Windows PowerShell 5.1, WinForms)
# Kopiert die Mods in den Spielordner von Graveyard Keeper 2 (BepInEx nur, wenn das Spiel noch keins hat).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Version = '1.2.0'
$OnlineUpdate = $true   # Nexus-Ausgabe: $false (keine Internetverbindung)
$ExeName = 'GraveyardKeeper2.exe'
$Root    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Assets  = Join-Path $Root 'installer\assets'
$De      = (Get-Culture).TwoLetterISOLanguageName -eq 'de'
if ($env:GK2_LANG) { $De = ($env:GK2_LANG -eq 'de') }   # nur zum Testen
function T($textDe, $textEn) { if ($script:De) { $textDe } else { $textEn } }   # Parameter nicht $de nennen: PowerShell unterscheidet keine Gross-/Kleinschreibung

# Paketaufbau erkennen (seit Spiel-Update 1.011 bringt das Spiel BepInEx selbst mit):
#  a) GitHub-ZIP: installer\files = Mod + Mod-Loader (winhttp.dll, doorstop, BepInEx\core)
#  b) Workshop:   BepInEx\plugins\GK2Tweaks\GK2Tweaks.dll direkt im Paket, ohne Mod-Loader -> nur der Mod wird installiert
$Payload = $null
foreach ($c in @((Join-Path $Root 'installer\files'), $Root)) {
    if (Test-Path (Join-Path $c 'BepInEx\plugins\GK2Tweaks\GK2Tweaks.dll')) { $Payload = $c; break }
}
# Quelle fuer den Mod-Loader: installer\loader (falls vorhanden) oder installer\files (GitHub-ZIP); sonst keine
$LoaderSrc = $null
foreach ($c in @((Join-Path $Root 'installer\loader'), (Join-Path $Root 'installer\files'))) {
    if (Test-Path (Join-Path $c 'winhttp.dll')) { $LoaderSrc = $c; break }
}
if (-not $Payload) {
    [System.Windows.Forms.MessageBox]::Show((T "Installationsdateien nicht gefunden.`n`nBitte die ZIP-Datei zuerst komplett entpacken (Rechtsklick > Alle extrahieren) und dann 'Installieren.bat' im entpackten Ordner starten." "Installer files not found.`n`nPlease extract the whole ZIP first (right click > Extract All) and run 'Installieren.bat' from the extracted folder."), 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    exit 1
}

# --- Spielordner automatisch finden (Steam-Bibliotheken) ---
# Pfade aus der Registry, aus libraryfolders.vdf oder aus dem Textfeld koennen kaputt sein (Anfuehrungszeichen,
# ungueltige Zeichen, abgestecktes Laufwerk). Join-Path/Test-Path wuerden dann wegen $ErrorActionPreference = 'Stop'
# den ganzen Installer abbrechen (GitHub #2). Deshalb Pfade nur ueber diese Helfer bauen und pruefen.
function Get-CleanPath($v) {
    if (-not $v) { return '' }
    return (([string]$v) -replace '[\x00-\x1f"]', '').Trim()
}
function Join-Safe($a, $b) {
    try { if (-not $a) { return '' }; return [System.IO.Path]::Combine([string]$a, [string]$b) } catch { return '' }
}
function Test-SafePath($p) {
    try {
        if (-not $p) { return $false }
        if (([string]$p).IndexOfAny([System.IO.Path]::GetInvalidPathChars()) -ge 0) { return $false }
        return [bool](Test-Path -LiteralPath $p -ErrorAction Stop)
    } catch { return $false }
}
function Test-GameDir($dir) { return [bool]($dir -and (Test-SafePath (Join-Safe $dir $ExeName))) }

function Find-Game {
    # Aus dem Steam-Workshop gestartet? <Bibliothek>\steamapps\workshop\content\4358690\<id>\ -> <Bibliothek>\steamapps\common\Graveyard Keeper 2
    try {
        $m = [regex]::Match($Root, '^(.*\\steamapps)\\workshop\\content\\4358690\\', 'IgnoreCase')
        if ($m.Success) {
            $g = Join-Safe $m.Groups[1].Value 'common\Graveyard Keeper 2'
            if (Test-GameDir $g) { return $g }
        }
    } catch {}
    # Steam traegt den Installationsort des Spiels (App 4358690) in die Programmliste von Windows ein
    foreach ($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 4358690', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 4358690') {
        try {
            $g = Get-CleanPath (Get-ItemProperty -Path $k -ErrorAction Stop).InstallLocation
            if (Test-GameDir $g) { return $g }
        } catch {}
    }
    $steamRoots = @()
    foreach ($k in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty -Path $k -ErrorAction Stop
            if ($p.SteamPath)   { $steamRoots += (Get-CleanPath ($p.SteamPath -replace '/', '\')) }
            if ($p.InstallPath) { $steamRoots += (Get-CleanPath $p.InstallPath) }
        } catch {}
    }
    $steamRoots += "${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam"
    $libs = @()
    foreach ($r in ($steamRoots | Where-Object { $_ } | Select-Object -Unique)) {
        $libs += $r
        try {
            $vdf = Join-Safe $r 'steamapps\libraryfolders.vdf'
            if (Test-SafePath $vdf) {
                foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw -ErrorAction Stop), '"path"\s+"([^"]+)"')) { $libs += (Get-CleanPath ($m.Groups[1].Value -replace '\\\\', '\')) }
            }
        } catch {}   # eine kaputte Bibliothek darf die Suche nicht abbrechen
    }
    foreach ($l in ($libs | Where-Object { $_ } | Select-Object -Unique)) {
        $g = Join-Safe $l 'steamapps\common\Graveyard Keeper 2'
        if (Test-GameDir $g) { return $g }
    }
    # Nichts gefunden: neben dem entpackten Ordner nachsehen (C:\Games\GK2-VanillaPlus-x\ -> C:\Games\Graveyard Keeper 2)
    try {
        $up = Split-Path -Parent $Root
        foreach ($c in @((Join-Safe $up 'Graveyard Keeper 2'), (Join-Safe (Split-Path -Parent $up) 'Graveyard Keeper 2'))) {
            if (Test-GameDir $c) { return $c }
        }
    } catch {}
    return ''
}

function Test-GameRunning {
    if (Get-Process -Name 'GraveyardKeeper2' -ErrorAction SilentlyContinue) {
        [System.Windows.Forms.MessageBox]::Show((T "Graveyard Keeper 2 läuft noch. Bitte das Spiel zuerst beenden." "Graveyard Keeper 2 is still running. Please quit the game first."), 'GK2 Vanilla+', 'OK', 'Warning') | Out-Null
        return $true
    }
    return $false
}

# --- Mod-Loader (BepInEx) ---
# Seit Spiel-Update 1.011 bringt das Spiel BepInEx selbst mit. Dessen Dateien (winhttp.dll, doorstop, BepInEx\core,
# BepInEx\config) duerfen wir nie ueberschreiben oder loeschen. Den Loader kopieren wir nur, wenn im Spielordner keiner
# liegt, und auch dann nur fehlende Dateien (vorhandene werden nie ueberschrieben). In loader-files.txt merken wir uns,
# welche Dateien von uns stammen, mit SHA256 ("Pfad|Hash"): "Alles entfernen" loescht nur diese, und nur, wenn sie
# seitdem unveraendert sind (z. B. nicht von Steam durch die Dateien des Spiels ersetzt).
# Gleicher Code in install.ps1 und update.ps1.
$ModDirs       = @('BepInEx\plugins\GK2Tweaks', 'BepInEx\plugins\GK2Ultrawide', 'BepInEx\GK2VanillaPlus')
$LoaderMarker  = 'BepInEx\GK2VanillaPlus\loader-files.txt'
$LoaderVersion = '5.4.23.5'   # mitgeliefertes BepInEx (installer\loader im GitHub-ZIP)
# BepInEx-Kern im Spielordner (BepInEx 5 oder 6)?
function Test-BepInExCore($dir) {
    foreach ($p in 'BepInEx\core\BepInEx.dll', 'BepInEx\core\BepInEx.Preloader.dll', 'BepInEx\core\BepInEx.Core.dll', 'BepInEx\core\BepInEx.Unity.Mono.Preloader.dll') {
        try { if (Test-Path -LiteralPath (Join-Path $dir $p)) { return $true } } catch {}
    }
    return $false
}
function Test-LoaderPresent($dir) {
    if ((Test-BepInExCore $dir) -or (Test-GameLoader $dir)) { return $true }
    # version.dll/winmm.dll: andere Doorstop-Namen - unsere winhttp.dll daneben wuerde BepInEx zweimal starten
    foreach ($p in 'winhttp.dll', 'version.dll', 'winmm.dll', 'doorstop_config.ini') {
        try { if (Test-Path -LiteralPath (Join-Path $dir $p)) { return $true } } catch {}
    }
    return $false
}
# BepInEx des Spiels: erkennbar am Workshop-Lader "GK2.WorkshopSync" (Spiel-Update 1.011)
function Test-GameLoader($dir) {
    try {
        $b = Join-Path $dir 'BepInEx'
        if (-not (Test-Path -LiteralPath $b)) { return $false }
        return [bool](Get-ChildItem -LiteralPath $b -Recurse -File -Force -Filter '*WorkshopSync*' -ErrorAction SilentlyContinue | Select-Object -First 1)
    } catch { return $false }
}
# Ist das BepInEx im Spielordner genau die Version, die wir mitliefern?
function Test-OurCoreVersion($dir) {
    try {
        $f = Join-Path $dir 'BepInEx\core\BepInEx.dll'
        if (-not (Test-Path -LiteralPath $f)) { return $false }
        $m = [regex]::Match([string](Get-Item -LiteralPath $f).VersionInfo.FileVersion, '\d+(\.\d+){1,3}')
        return ($m.Success -and ([version]$m.Value) -eq ([version]$LoaderVersion))
    } catch { return $false }
}
# Liegt unter BepInEx\patchers etwas, das nicht von uns stammt (z. B. der Lader des Spiels oder anderer Mods)?
function Test-ForeignPatchers($dir, $entries) {
    try {
        $p = Join-Path $dir 'BepInEx\patchers'
        if (-not (Test-Path -LiteralPath $p)) { return $false }
        foreach ($fi in @(Get-ChildItem -LiteralPath $p -Recurse -File -Force -ErrorAction SilentlyContinue)) {
            if (-not (@($entries) -contains (Get-RelPath $dir $fi.FullName))) { return $true }
        }
        return $false
    } catch { return $true }
}
# 'none'    = kein Loader
# 'ours'    = von diesem Installer eingerichtet (loader-files.txt, unsere BepInEx-Version, kein Lader des Spiels, keine fremden Patcher)
# 'legacy'  = von einem Installer vor 1.7.5 eingerichtet (Mod < 1.7.5 installiert, unsere BepInEx-Version, noch ohne loader-files.txt)
# 'foreign' = vom Spiel oder von anderswo - wird nie veraendert
# 'partial' = nur eine Proxy-Datei (winhttp.dll/version.dll/winmm.dll/doorstop_config.ini) ohne BepInEx-Kern: der Mod wird
#             installiert, laedt aber nicht; der Installer warnt (kein eigener Loader daneben, sonst evtl. zwei Doorstops)
function Get-LoaderMode($dir) {
    if (-not (Test-LoaderPresent $dir)) { return 'none' }
    if (Test-GameLoader $dir) { return 'foreign' }
    if (-not (Test-BepInExCore $dir)) { return 'partial' }
    if (-not (Test-OurCoreVersion $dir)) { return 'foreign' }
    try {
        if (Test-Path -LiteralPath (Join-Path $dir $LoaderMarker)) {
            if (Test-ForeignPatchers $dir (Read-LoaderMarker $dir)) { return 'foreign' }
            return 'ours'
        }
        $have = Get-InstalledVersion $dir
        if ($have -ne $null -and $have -lt [version]'1.7.5' -and -not (Test-ForeignPatchers $dir @())) { return 'legacy' }
    } catch {}
    return 'foreign'
}
function Get-FileSha($path) {
    try { return [string](Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash } catch { return '' }
}
# loader-files.txt lesen: Zeilen "relativer\Pfad|SHA256" (Hash optional); Plugins anderer Mods stehen nie darin.
# Rueckgabe: @{ Files = relative Pfade; Hashes = Pfad -> SHA256 beim Installieren }
function Read-LoaderInfo($dir) {
    $files = New-Object System.Collections.Generic.List[string]
    $hashes = @{}
    try {
        $m = Join-Path $dir $LoaderMarker
        if (Test-Path -LiteralPath $m) {
            foreach ($l in @(Get-Content -LiteralPath $m -Encoding UTF8)) {
                $line = ([string]$l).Trim()
                if (-not $line -or $line.StartsWith('#')) { continue }
                $hash = ''
                $bar = $line.IndexOf('|')
                if ($bar -ge 0) { $hash = $line.Substring($bar + 1).Trim(); $line = $line.Substring(0, $bar) }
                $r = $line.Trim().Replace('/', '\').TrimStart('\')
                if (-not $r -or $r.Contains('..') -or $r.Contains(':')) { continue }
                if ($r.StartsWith('BepInEx\plugins\', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
                if (-not $files.Contains($r)) { $files.Add($r) }
                if ($hash) { $hashes[$r] = $hash }
            }
        }
    } catch {}
    return @{ Files = $files.ToArray(); Hashes = $hashes }
}
# Liste der von uns installierten Loader-Dateien (relative Pfade)
function Read-LoaderMarker($dir) { return (Read-LoaderInfo $dir).Files }
# Pfad -> SHA256 beim Installieren (fehlt = unbekannt)
function Read-LoaderHashes($dir) { return (Read-LoaderInfo $dir).Hashes }
function Write-LoaderMarker($dir, $entries, $hashes) {
    $m = Join-Path $dir $LoaderMarker
    $d = Split-Path -Parent $m
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# GK2 Vanilla+: BepInEx files placed by the Vanilla+ installer (path|SHA256). "Remove everything" deletes only these, and only if unchanged.')
    foreach ($e in @($entries)) {
        if (-not $e) { continue }
        $h = ''
        if ($hashes -and $hashes.ContainsKey([string]$e)) { $h = [string]$hashes[[string]$e] }
        if ($h) { $lines.Add([string]$e + '|' + $h) } else { $lines.Add([string]$e) }
    }
    [System.IO.File]::WriteAllLines($m, $lines.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
}
# 'legacy': Loader-Dateien, die ein Installer vor 1.7.5 kopiert hat = Dateien aus dem mitgelieferten Loader, die im
# Spielordner byte-gleich liegen. Geaenderte (z. B. BepInEx.cfg) zaehlen nicht dazu und bleiben beim Entfernen stehen.
function Get-LegacyLoaderFiles($loaderSrc, $dir) {
    $files = New-Object System.Collections.Generic.List[string]
    $hashes = @{}
    if (-not $loaderSrc) { return @{ Files = $files.ToArray(); Hashes = $hashes } }
    foreach ($fi in @(Get-ChildItem -LiteralPath $loaderSrc -Recurse -File -Force)) {
        $rel = Get-RelPath $loaderSrc $fi.FullName
        if ((Test-ModPath $rel) -or $rel.StartsWith('BepInEx\plugins\', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        $dst = Join-Path $dir $rel
        if (-not (Test-Path -LiteralPath $dst -PathType Leaf)) { continue }
        $h = Get-FileSha $dst
        if ($h -and $h -eq (Get-FileSha $fi.FullName)) { $files.Add($rel); $hashes[$rel] = $h }
    }
    return @{ Files = $files.ToArray(); Hashes = $hashes }
}
function Get-RelPath($base, $full) {
    $b = [System.IO.Path]::GetFullPath($base).TrimEnd('\') + '\'
    return [System.IO.Path]::GetFullPath($full).Substring($b.Length)
}
function Test-ModPath($rel) {
    foreach ($p in $ModDirs) { if ($rel.StartsWith($p + '\', [System.StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}
function Copy-One($src, $dst) {
    $d = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }
    try { Unblock-File -LiteralPath $src } catch {}
    Copy-Item -LiteralPath $src -Destination $dst -Force
}
# Kopiert immer die Mod-Dateien; vom Loader nur fehlende Dateien und nur bei 'none', 'ours' (Reparatur) oder 'legacy',
# nie bei 'foreign'/'partial'. Vorhandene Loader-Dateien werden nie ueberschrieben.
# Rueckgabe: Loader-Modus vor dem Kopieren; 'missing' = weder im Spielordner noch im Paket ein Loader (nichts kopiert)
function Install-Payload($payload, $loaderSrc, $dir) {
    if ([System.IO.Path]::GetFullPath($payload).TrimEnd('\') -eq [System.IO.Path]::GetFullPath($dir).TrimEnd('\')) { throw (T 'Das Paket liegt im Spielordner selbst.' 'The package is inside the game folder itself.') }
    $mode = Get-LoaderMode $dir
    if ($mode -eq 'none' -and -not $loaderSrc) { return 'missing' }
    foreach ($fi in @(Get-ChildItem -LiteralPath $payload -Recurse -File -Force)) {
        $rel = Get-RelPath $payload $fi.FullName
        if ((Test-ModPath $rel) -and $rel -ne $LoaderMarker) { Copy-One $fi.FullName (Join-Path $dir $rel) }
    }
    if ($mode -eq 'foreign' -or $mode -eq 'partial' -or -not $loaderSrc) { return $mode }
    $placed = New-Object System.Collections.Generic.List[string]
    $hashes = @{}
    if ($mode -eq 'ours') {
        foreach ($r in @(Read-LoaderMarker $dir)) { if (-not $placed.Contains($r)) { $placed.Add($r) } }
        $old = Read-LoaderHashes $dir
        foreach ($k in @($old.Keys)) { $hashes[$k] = $old[$k] }
    }
    if ($mode -eq 'legacy') {
        $adopt = Get-LegacyLoaderFiles $loaderSrc $dir
        foreach ($r in @($adopt.Files)) { if (-not $placed.Contains($r)) { $placed.Add($r) }; $hashes[$r] = $adopt.Hashes[$r] }
    }
    foreach ($fi in @(Get-ChildItem -LiteralPath $loaderSrc -Recurse -File -Force)) {
        $rel = Get-RelPath $loaderSrc $fi.FullName
        if ((Test-ModPath $rel) -or $rel.StartsWith('BepInEx\plugins\', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        $dst = Join-Path $dir $rel
        if (Test-Path -LiteralPath $dst) { continue }   # vorhandene Datei (auch Einstellungen) nie ueberschreiben
        Copy-One $fi.FullName $dst
        if (-not $placed.Contains($rel)) { $placed.Add($rel) }
        $hashes[$rel] = Get-FileSha $dst
    }
    Write-LoaderMarker $dir $placed.ToArray() $hashes
    return $mode
}

# --- Aussehen: Farben, Schriften, Bilder ---
function C($hex) { return [System.Drawing.ColorTranslator]::FromHtml($hex) }
$ColBg     = C '#1a1c23'   # Fensterhintergrund
$ColCard   = C '#22252e'   # Felder / Karten
$ColCardHi = C '#2a2d38'   # gewaehlte Karte
$ColLine   = C '#3a3e4c'   # Rahmen
$ColText   = C '#ece0c4'   # Text (Pergament)
$ColMuted  = C '#9a927f'   # Nebentext
$ColGold   = C '#e0a94a'   # Akzent (wie "Ultrawide · Performance · QoL" im Logo)
$ColRed    = C '#8e2a20'   # Hauptknopf
$ColGreen  = C '#78c86a'   # Plus im Logo
function F($size, $bold) { if ($bold) { New-Object System.Drawing.Font('Segoe UI', $size, [System.Drawing.FontStyle]::Bold) } else { New-Object System.Drawing.Font('Segoe UI', $size) } }

function Load-Img($name) {
    $p = Join-Path $Assets $name
    if (Test-Path $p) { try { return (New-Object System.Drawing.Bitmap($p)) } catch {} }
    return $null
}
$Banner  = Load-Img 'banner.png'   # 636x200, transparent; sichtbarer Teil siehe $BannerSrc
$IconImg = Load-Img 'icon.png'
$BannerSrc = New-Object System.Drawing.Rectangle(88, 22, 436, 158)

function New-RoundRect([System.Drawing.Rectangle]$r, [int]$rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $rad * 2
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return ,$p
}

# Abgerundete Flaeche mit Rahmen (fuer Felder und Karten)
function Draw-Box($g, $w, $h, $fill, $line, $lineWidth) {
    $g.SmoothingMode = 'AntiAlias'
    $o = [int][Math]::Ceiling($lineWidth / 2)
    $path = New-RoundRect (New-Object System.Drawing.Rectangle($o, $o, ($w - 2 * $o - 1), ($h - 2 * $o - 1))) 6
    $b = New-Object System.Drawing.SolidBrush($fill)
    $g.FillPath($b, $path)
    $pen = New-Object System.Drawing.Pen($line, $lineWidth)
    $g.DrawPath($pen, $path)
    $b.Dispose(); $pen.Dispose(); $path.Dispose()
}

# Knoepfe: 'primary' (rot, gross), 'danger' (rot), 'ghost' (dunkel mit Rahmen)
function New-Btn($text, $kind) {
    $btn = New-Object System.Windows.Forms.Button
    $btn.Text = $text
    $btn.FlatStyle = 'Flat'
    $btn.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btn.UseVisualStyleBackColor = $false
    if ($kind -eq 'primary' -or $kind -eq 'danger') {
        $btn.BackColor = $ColRed
        $btn.ForeColor = [System.Drawing.Color]::White
        $btn.FlatAppearance.BorderSize = 0
        $btn.FlatAppearance.MouseOverBackColor = C '#a8382b'
        $btn.FlatAppearance.MouseDownBackColor = C '#6f2018'
        $btn.Font = F $(if ($kind -eq 'primary') { 11.5 } else { 10 }) $true
    } else {
        $btn.BackColor = $ColCard
        $btn.ForeColor = $ColText
        $btn.FlatAppearance.BorderSize = 1
        $btn.FlatAppearance.BorderColor = $ColLine
        $btn.FlatAppearance.MouseOverBackColor = C '#30343f'
        $btn.FlatAppearance.MouseDownBackColor = $ColLine
        $btn.Font = F 10 $false
    }
    return $btn
}

function New-Text($text, $x, $y, $w, $h, $size, $bold, $color) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text
    $l.SetBounds($x, $y, $w, $h)
    $l.Font = F $size $bold
    $l.ForeColor = $color
    $l.BackColor = [System.Drawing.Color]::Transparent
    return $l
}

# --- Fenster ---
$W = 720; $Pad = 28; $Inner = $W - 2 * $Pad
$form = New-Object System.Windows.Forms.Form
$form.Text = "GK2 Vanilla+ $Version  ·  by McFly7"
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.Font = F 10 $false
$form.BackColor = $ColBg
$form.ForeColor = $ColText
if ($IconImg) { try { $form.Icon = [System.Drawing.Icon]::FromHandle((New-Object System.Drawing.Bitmap($IconImg, 64, 64)).GetHicon()) } catch {} }

# Kopfbereich: Nachthimmel, Logo, Versionsabzeichen
$header = New-Object System.Windows.Forms.Panel
$header.SetBounds(0, 0, $W, 190)
$rnd = New-Object System.Random(7)
$Stars = @(); for ($i = 0; $i -lt 34; $i++) {
    $sx = $rnd.Next(8, $W - 8); $sy = $rnd.Next(8, 176)
    if ($sx -gt 128 -and $sx -lt 592) { continue }   # nicht hinter dem Schriftzug
    $Stars += ,@($sx, $sy, $(if ($rnd.Next(0, 5) -eq 0) { 3 } else { 2 }), $rnd.Next(70, 200))
}
$header.Add_Paint({
    $g = $_.Graphics
    $rc = $this.ClientRectangle
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rc, (C '#22305a'), (C '#14161f'), [single]90)
    $g.FillRectangle($bg, $rc); $bg.Dispose()
    foreach ($s in $Stars) {
        $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($s[3], 240, 232, 200))
        $g.FillRectangle($sb, $s[0], $s[1], $s[2], $s[2]); $sb.Dispose()
    }
    if ($Banner) {
        $g.InterpolationMode = 'NearestNeighbor'
        $g.PixelOffsetMode = 'Half'
        $dst = New-Object System.Drawing.Rectangle([int](($rc.Width - $BannerSrc.Width) / 2), 14, $BannerSrc.Width, $BannerSrc.Height)
        $g.DrawImage($Banner, $dst, $BannerSrc, [System.Drawing.GraphicsUnit]::Pixel)
    } else {
        $g.TextRenderingHint = 'AntiAlias'
        $fb = New-Object System.Drawing.SolidBrush($ColText)
        $g.DrawString('GK2 Vanilla+', (F 30 $true), $fb, [single]($Pad), [single]50)
        $g.DrawString('Ultrawide · Performance · QoL', (F 12 $false), (New-Object System.Drawing.SolidBrush($ColGold)), [single]($Pad + 4), [single]110)
        $fb.Dispose()
    }
    # Versionsabzeichen oben rechts
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAlias'
    $vf = F 9 $true
    $vt = "v$Version"
    $sz = $g.MeasureString($vt, $vf)
    $bw = [int]$sz.Width + 16
    $pill = New-RoundRect (New-Object System.Drawing.Rectangle(($rc.Width - $bw - 14), 12, $bw, 22)) 10
    $pb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 10, 12, 20))
    $g.FillPath($pb, $pill); $pb.Dispose()
    $pp = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, $ColGold.R, $ColGold.G, $ColGold.B), 1)
    $g.DrawPath($pp, $pill); $pp.Dispose()
    $tb = New-Object System.Drawing.SolidBrush($ColGold)
    $g.DrawString($vt, $vf, $tb, [single]($rc.Width - $bw - 14 + 8), [single]14.5); $tb.Dispose()
    $mb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(170, $ColText.R, $ColText.G, $ColText.B))
    $g.DrawString('by McFly7', (F 8.5 $false), $mb, [single]($rc.Width - 82), [single]($rc.Height - 24)); $mb.Dispose()
    # Goldene Kante unten
    $gl = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(140, $ColGold.R, $ColGold.G, $ColGold.B))
    $g.FillRectangle($gl, 0, $rc.Height - 2, $rc.Width, 2); $gl.Dispose()
})
$form.Controls.Add($header)

# Spielordner
$y = 206
$form.Controls.Add((New-Text (T 'SPIELORDNER' 'GAME FOLDER') $Pad $y 200 18 8.5 $true $ColGold))
$info = New-Text (T 'Der Ordner mit GraveyardKeeper2.exe – wird automatisch gesucht' 'The folder containing GraveyardKeeper2.exe – found automatically') ($Pad + 200) $y ($Inner - 200) 18 8.5 $false $ColMuted
$info.TextAlign = 'TopRight'
$form.Controls.Add($info)

$folder = New-Object System.Windows.Forms.Panel
$folder.SetBounds($Pad, ($y + 22), $Inner, 40)
$folder.Add_Paint({ Draw-Box $_.Graphics $this.Width $this.Height $ColCard $ColLine 1 })
$box = New-Object System.Windows.Forms.TextBox
$box.BorderStyle = 'None'
$box.BackColor = $ColCard
$box.ForeColor = $ColText
$box.Font = F 10 $false
$box.SetBounds(14, 11, ($Inner - 160), 20)
$browse = New-Btn (T 'Durchsuchen …' 'Browse …') 'ghost'
$browse.SetBounds(($Inner - 134), 5, 128, 30)
$folder.Controls.AddRange(@($box, $browse))
$form.Controls.Add($folder)

# Statusfeld: farbiger Streifen links (Farbe = Statusfarbe)
$y += 74
$statusBox = New-Object System.Windows.Forms.Panel
$statusBox.SetBounds($Pad, $y, $Inner, 74)
$status = New-Text '' 20 9 ($Inner - 32) 58 9.5 $false $ColText
$statusBox.Controls.Add($status)
$statusBox.Add_Paint({
    $g = $_.Graphics
    Draw-Box $g $this.Width $this.Height $ColCard $ColLine 1
    $ab = New-Object System.Drawing.SolidBrush($status.ForeColor)
    $g.FillRectangle($ab, 1, 8, 4, $this.Height - 16); $ab.Dispose()
})
$status.Add_ForeColorChanged({ $statusBox.Invalidate() })
$form.Controls.Add($statusBox)
$y += 74

# Welche Version? Stabil = dieses Paket (offline), Beta = neueste Vorabversion von GitHub
$rbStable = New-Object System.Windows.Forms.RadioButton
$rbStable.Checked = $true
$rbBeta = New-Object System.Windows.Forms.RadioButton
$CardW = [int](($Inner - 12) / 2)
# Die RadioButtons halten nur den Zustand; Karten zeichnen Titel und Auswahlkreis selbst
function Select-Channel($beta) {
    $rbBeta.Checked = [bool]$beta
    $rbStable.Checked = -not $beta
    $cardStable.Invalidate(); $cardBeta.Invalidate()
}
function New-Card($rb, $x, $cy, $title, $desc, $badge) {
    $p = New-Object System.Windows.Forms.Panel
    $p.SetBounds($x, $cy, $CardW, 74)
    $p.Cursor = [System.Windows.Forms.Cursors]::Hand
    $p.Tag = @{ Rb = $rb; Title = $title; Badge = $badge; Beta = ($rb -eq $rbBeta) }
    $d = New-Text $desc 40 41 ($CardW - 52) 24 9 $false $ColMuted
    $d.Cursor = [System.Windows.Forms.Cursors]::Hand
    $d.Add_Click({ Select-Channel $this.Parent.Tag.Beta })
    $p.Add_Click({ Select-Channel $this.Tag.Beta })
    $p.Add_Paint({
        $g = $_.Graphics
        $sel = $this.Tag.Rb.Checked
        if ($sel) { Draw-Box $g $this.Width $this.Height $ColCardHi $ColGold 2 } else { Draw-Box $g $this.Width $this.Height $ColCard $ColLine 1 }
        $g.SmoothingMode = 'AntiAlias'
        $g.TextRenderingHint = 'AntiAlias'
        # Auswahlkreis
        $rp = New-Object System.Drawing.Pen($(if ($sel) { $ColGold } else { $ColMuted }), 2)
        $g.DrawEllipse($rp, 16, 17, 16, 16); $rp.Dispose()
        if ($sel) { $rf = New-Object System.Drawing.SolidBrush($ColGold); $g.FillEllipse($rf, 20, 21, 8, 8); $rf.Dispose() }
        $tb = New-Object System.Drawing.SolidBrush($ColText)
        $g.DrawString($this.Tag.Title, (F 12 $true), $tb, [single]38, [single]13); $tb.Dispose()
        if ($this.Tag.Badge) {
            $bf = F 7.5 $true
            $sz = $g.MeasureString($this.Tag.Badge, $bf)
            $bw = [int]$sz.Width + 12
            $pill = New-RoundRect (New-Object System.Drawing.Rectangle(($this.Width - $bw - 12), 15, $bw, 19)) 9
            $pb = New-Object System.Drawing.SolidBrush((C '#2f5a2a'))
            $g.FillPath($pb, $pill); $pb.Dispose()
            $bb = New-Object System.Drawing.SolidBrush((C '#bfe8b0'))
            $g.DrawString($this.Tag.Badge, $bf, $bb, [single]($this.Width - $bw - 12 + 6), [single]17.5); $bb.Dispose()
        }
    })
    $p.Controls.Add($d)
    return $p
}
$chanLabel = New-Text (T 'VERSION' 'VERSION') $Pad ($y + 16) 200 18 8.5 $true $ColGold
$cardStable = New-Card $rbStable $Pad ($y + 38) (T 'Stabil' 'Stable') (T 'Getestete Version · direkt aus diesem Paket' 'Tested version · straight from this package') (T 'EMPFOHLEN' 'RECOMMENDED')
$cardBeta   = New-Card $rbBeta ($Pad + $CardW + 12) ($y + 38) 'Beta' (T 'Neueste Funktionen · lädt von GitHub' 'Newest features · downloads from GitHub') ''
$rbHint = New-Text (T "Beta-Versionen können Fehler enthalten. Den Kanal kannst du später im Mod-Menü (F9) unter 'Update-Kanal' ändern." "Beta versions may contain bugs. You can change the channel later in the mod menu (F9) under 'Update channel'.") $Pad ($y + 118) $Inner 20 8.5 $false $ColMuted
if ($OnlineUpdate) { $form.Controls.AddRange(@($chanLabel, $cardStable, $cardBeta, $rbHint)); $y += 140 }

# Installierte Version lesen (aus der DLL-Dateiversion)
function Get-InstalledVersion($dir) {
    foreach ($p in 'BepInEx\plugins\GK2Tweaks\GK2Tweaks.dll', 'BepInEx\plugins\GK2Ultrawide\GK2Ultrawide.dll') {
        $f = Join-Path $dir $p
        if (Test-Path $f) {
            try {
                $v = [version](Get-Item $f).VersionInfo.FileVersion
                if ($p -like '*GK2Ultrawide*') { return [version]'1.0.0' }   # nur Ultrawide 1.0 installiert
                return [version]('{0}.{1}.{2}' -f $v.Major, $v.Minor, [Math]::Max(0, $v.Build))
            } catch { return [version]'0.0.0' }
        }
    }
    return $null
}

# Update-Kanal in die Mod-Einstellungen schreiben (BepInEx\config\mats.gk2.tweaks.cfg, [Interface] UpdateChannel)
function Set-UpdateChannel($dir, $channel) {
    try {
        $cfg = Join-Path $dir 'BepInEx\config\mats.gk2.tweaks.cfg'
        $line = "UpdateChannel = $channel"
        $lines = New-Object System.Collections.Generic.List[string]
        if (Test-Path $cfg) { foreach ($l in (Get-Content $cfg -Encoding UTF8)) { $lines.Add($l) } }
        $idx = -1
        for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^\s*UpdateChannel\s*=') { $idx = $i; break } }
        if ($idx -ge 0) { $lines[$idx] = $line }
        else {
            $sec = -1
            for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i].Trim() -eq '[Interface]') { $sec = $i; break } }
            if ($sec -ge 0) { $lines.Insert($sec + 1, $line) } else { $lines.Add('[Interface]'); $lines.Add(''); $lines.Add($line) }
        }
        New-Item -ItemType Directory -Force -Path (Split-Path $cfg) | Out-Null
        [System.IO.File]::WriteAllLines($cfg, $lines.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
    } catch {}
}

function Test-Beta { return ($OnlineUpdate -and $rbBeta.Checked) }

$ColOk   = C '#8fd27e'
$ColWarn = C '#f0c070'
$ColBad  = C '#f0907a'
function Set-Status {
    if (Test-GameDir $box.Text) {
        $have = Get-InstalledVersion $box.Text
        $pkg  = [version]$Version
        $mode = Get-LoaderMode $box.Text
        $status.ForeColor = $ColOk
        if (Test-Beta) {
            $install.Text = T 'Beta installieren' 'Install beta'
            $haveText = if ($have -eq $null) { T 'noch nicht installiert' 'not installed yet' } else { "$have" }
            $status.ForeColor = $ColWarn
            $status.Text = T "Beta-Version (Vorabversion): wird aus dem Internet geladen (installiert: $haveText). Sie kann Fehler enthalten – bitte vorher Spielstand sichern. Einstellungen bleiben erhalten." "Beta (pre-release): downloaded from the internet (installed: $haveText). It may contain bugs – please back up your saves first. Your settings are kept."
        } elseif ($mode -eq 'none' -and -not $LoaderSrc) {
            $install.Text = T 'Installieren' 'Install'
            $status.ForeColor = $ColBad
            $status.Text = T "Dieses Paket enthält kein BepInEx (Mod-Loader), und im Spielordner ist keins. Seit Spiel-Update 1.011 bringt das Spiel es mit: bitte das Spiel über Steam aktualisieren oder das komplette Paket von GitHub laden." "This package contains no BepInEx (mod loader) and the game folder has none. Since game update 1.011 the game ships it: please update the game via Steam or download the full package from GitHub."
        } elseif ($have -eq $null) {
            $install.Text = T 'Installieren' 'Install'
            $status.Text = T "Spiel gefunden. Der Mod ist noch nicht installiert." "Game found. The mod is not installed yet."
        } elseif ($have -lt $pkg) {
            $install.Text = T 'Aktualisieren' 'Update'
            $status.Text = T "Update verfügbar: installiert ist $have, dieses Paket ist $Version.`nDeine Einstellungen bleiben erhalten." "Update available: $have is installed, this package is $Version.`nYour settings are kept."
        } elseif ($have -eq $pkg) {
            $install.Text = T 'Neu installieren' 'Reinstall'
            $status.Text = T "Version $Version ist bereits installiert. 'Neu installieren' repariert die Installation, Einstellungen bleiben erhalten." "Version $Version is already installed. 'Reinstall' repairs the installation, settings are kept."
        } else {
            $install.Text = T 'Trotzdem installieren' 'Install anyway'
            $status.ForeColor = $ColWarn
            $status.Text = T "Installiert ist eine neuere Version ($have). Dieses Paket ($Version) ist älter." "A newer version ($have) is installed. This package ($Version) is older."
        }
        if ($mode -eq 'foreign' -and -not (Test-Beta)) { $status.Text += T "`nVorhandenes BepInEx (z. B. vom Spiel) wird verwendet und nicht verändert." "`nThe existing BepInEx (e.g. the game's own) is used and left unchanged." }
        if ($mode -eq 'partial' -and -not (Test-Beta)) {
            $status.ForeColor = $ColWarn
            $status.Text += T "`nIm Spielordner liegt eine Mod-Loader-Datei (winhttp.dll, version.dll, winmm.dll oder doorstop_config.ini), aber kein BepInEx. Der Mod lädt erst, wenn BepInEx vollständig ist (Spiel in Steam aktualisieren bzw. Dateien prüfen)." "`nThe game folder has a mod loader file (winhttp.dll, version.dll, winmm.dll or doorstop_config.ini) but no BepInEx. The mod only loads once BepInEx is complete (update the game in Steam or verify its files)."
        }
    } else {
        $install.Text = T 'Installieren' 'Install'
        $status.ForeColor = $ColBad
        $status.Text = T 'In diesem Ordner liegt keine GraveyardKeeper2.exe. Bitte den Spielordner auswählen (Steam: Rechtsklick auf das Spiel > Verwalten > Lokale Dateien durchsuchen).' 'No GraveyardKeeper2.exe in this folder. Please select the game folder (Steam: right click the game > Manage > Browse local files).'
    }
    $statusBox.Invalidate()
}
$box.Add_TextChanged({
    # "Als Pfad kopieren" im Explorer fuegt Anfuehrungszeichen ein - entfernen, sonst wird der Ordner nicht erkannt
    if ($box.Text -match '[\x00-\x1f"]') { $box.Text = Get-CleanPath $box.Text; $box.SelectionStart = $box.Text.Length; return }
    Set-Status
})
$rbStable.Add_CheckedChanged({ Set-Status })
$rbBeta.Add_CheckedChanged({ Set-Status })

$browse.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = T 'Spielordner von Graveyard Keeper 2 auswählen' 'Select the Graveyard Keeper 2 game folder'
    if (Test-SafePath $box.Text) { $dlg.SelectedPath = $box.Text }
    if ($dlg.ShowDialog() -eq 'OK') { $box.Text = $dlg.SelectedPath }
})

# Knopfleiste
$y += 22
$install   = New-Btn (T 'Installieren' 'Install') 'primary'
$install.SetBounds($Pad, $y, 268, 48)
$uninstall = New-Btn (T 'Deinstallieren' 'Uninstall') 'ghost'
$uninstall.SetBounds(($Pad + 280), $y, 170, 48)
$close     = New-Btn (T 'Schließen' 'Close') 'ghost'
$close.SetBounds(($W - $Pad - 130), $y, 130, 48)
$form.Controls.AddRange(@($install, $uninstall, $close))
$form.AcceptButton = $install
$form.Add_Shown({ $install.Focus(); $box.SelectionStart = $box.Text.Length; $box.SelectionLength = 0 })
$y += 48 + 22

# Fusszeile: Online-Update links, Links rechts
$footer = New-Object System.Windows.Forms.Panel
$footer.SetBounds(0, $y, $W, 50)
$footer.BackColor = C '#13151b'
$footer.Add_Paint({
    $lp = New-Object System.Drawing.Pen($ColLine, 1)
    $_.Graphics.DrawLine($lp, 0, 0, $this.Width, 0); $lp.Dispose()
})
$online    = New-Btn (T 'Online nach Updates suchen' 'Check online for updates') 'ghost'
$online.SetBounds($Pad, 10, 250, 30)
$credit = New-Object System.Windows.Forms.LinkLabel
$kofiText = T '♥ Unterstützen' '♥ Support'
$credit.Text = "$kofiText     GitHub"
[void]$credit.Links.Clear()
[void]$credit.Links.Add(0, $kofiText.Length, 'https://ko-fi.com/mcfly7')
[void]$credit.Links.Add($kofiText.Length + 5, 6, 'https://github.com/Matshio7/gk2-vanilla-plus')
$credit.Font = F 9.5 $false
$credit.LinkColor = $ColGold
$credit.ActiveLinkColor = $ColText
$credit.LinkBehavior = 'HoverUnderline'
$credit.BackColor = [System.Drawing.Color]::Transparent
$credit.TextAlign = 'MiddleRight'
$credit.SetBounds(($W - $Pad - 260), 14, 260, 22)
$credit.Add_LinkClicked({ Start-Process $_.Link.LinkData })
$footer.Controls.Add($credit)
if ($OnlineUpdate) { $footer.Controls.Add($online) }
$form.Controls.Add($footer)
$form.ClientSize = New-Object System.Drawing.Size($W, ($y + 50))

$online.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    & (Join-Path $Payload 'BepInEx\GK2VanillaPlus\update.ps1') -GameDir $dir -Ask -Channel $(if (Test-Beta) { 'beta' } else { 'stable' })
    Set-Status
})
$close.Add_Click({ $form.Close() })

$install.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    if (Test-Beta) {
        & (Join-Path $Payload 'BepInEx\GK2VanillaPlus\update.ps1') -GameDir $dir -Ask -Channel beta
        Set-Status
        return
    }
    try {
        # Mod-Dateien immer; BepInEx nur, wenn im Spielordner noch keins liegt (das des Spiels bleibt unveraendert)
        $mode = Install-Payload $Payload $LoaderSrc $dir
        if ($mode -eq 'missing') {
            [System.Windows.Forms.MessageBox]::Show((T "Dieses Paket enthält kein BepInEx (Mod-Loader), und im Spielordner ist auch keins.`n`nSeit Spiel-Update 1.011 bringt Graveyard Keeper 2 BepInEx selbst mit: bitte das Spiel über Steam aktualisieren. Für ältere Spielversionen das komplette Paket von GitHub laden:`nhttps://github.com/Matshio7/gk2-vanilla-plus/releases/latest" "This package contains no BepInEx (mod loader) and the game folder has none either.`n`nSince game update 1.011 Graveyard Keeper 2 ships BepInEx itself: please update the game via Steam. For older game versions download the full package from GitHub:`nhttps://github.com/Matshio7/gk2-vanilla-plus/releases/latest"), 'GK2 Vanilla+', 'OK', 'Warning') | Out-Null
            Set-Status
            return
        }
        # Stabil gewaehlt: Kanal merken, Beta-Markierung entfernen
        if ($OnlineUpdate) { Set-UpdateChannel $dir 'Stable' }
        $mark = Join-Path $dir 'BepInEx\GK2VanillaPlus\installed-tag.txt'
        if (Test-Path $mark) { Remove-Item $mark -Force -ErrorAction SilentlyContinue }
        $msg = T "Fertig! Version $Version ist installiert.`n`nSpiel ganz normal über Steam starten. Im Spiel öffnet F9 das Mod-Menü, F10 die FPS-Anzeige." "Done! Version $Version is installed.`n`nStart the game normally via Steam. In game, F9 opens the mod menu, F10 the FPS display."
        if ($mode -eq 'foreign') { $msg += T "`n`nDas vorhandene BepInEx (z. B. das des Spiels) wurde verwendet und nicht verändert." "`n`nThe existing BepInEx (e.g. the game's own) was used and left unchanged." }
        $icon = 'Information'
        if ($mode -eq 'partial') {
            # Nur eine Proxy-Datei, kein BepInEx-Kern: kein Erfolgstext, der Mod laedt so nicht
            $icon = 'Warning'
            $msg = T "Version $Version wurde kopiert, aber im Spielordner fehlt BepInEx: Dort liegt nur eine Mod-Loader-Datei (winhttp.dll, version.dll, winmm.dll oder doorstop_config.ini) ohne den Ordner BepInEx\core. Vanilla+ lädt erst, wenn BepInEx vollständig ist.`n`nSeit Spiel-Update 1.011 bringt das Spiel BepInEx mit: in Steam Rechtsklick auf das Spiel > Eigenschaften > Installierte Dateien > Dateien auf Fehler überprüfen." "Version $Version was copied, but BepInEx is missing in the game folder: there is only a mod loader file (winhttp.dll, version.dll, winmm.dll or doorstop_config.ini) without the BepInEx\core folder. Vanilla+ only loads once BepInEx is complete.`n`nSince game update 1.011 the game ships BepInEx: in Steam right click the game > Properties > Installed Files > Verify integrity of game files."
        }
        [System.Windows.Forms.MessageBox]::Show($msg, 'GK2 Vanilla+', 'OK', $icon) | Out-Null
        Set-Status
    } catch {
        [System.Windows.Forms.MessageBox]::Show((T "Fehler beim Kopieren:`n" "Copy failed:`n") + $_.Exception.Message + (T "`n`nTipp: Installieren.bat per Rechtsklick 'Als Administrator ausführen', falls das Spiel unter 'Programme' liegt." "`n`nTip: right click Installieren.bat > 'Run as administrator' if the game is inside 'Program Files'."), 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    }
})

# Auswahl beim Deinstallieren: 'all' (alles entfernen), 'mods' (nur Vanilla+) oder 'cancel'
# $ownLoader: BepInEx wurde von diesem Installer eingerichtet (loader-files.txt) - nur dann entfernt "Alles entfernen" es
function Ask-Remove($snapshot, $ownLoader) {
    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'GK2 Vanilla+'
    $f.ClientSize = New-Object System.Drawing.Size(560, 300)
    $f.StartPosition = 'CenterParent'
    $f.FormBorderStyle = 'FixedDialog'
    $f.MaximizeBox = $false; $f.MinimizeBox = $false
    $f.Font = F 10 $false
    $f.BackColor = $ColBg
    $f.ForeColor = $ColText
    if ($form.Icon) { $f.Icon = $form.Icon }
    $f.Controls.Add((New-Text (T 'Was soll entfernt werden?' 'What should be removed?') 24 18 510 28 13 $true $ColText))
    $f.Controls.Add((New-Text (T 'ALLES ENTFERNEN' 'REMOVE EVERYTHING') 24 60 510 18 8.5 $true $ColGold))
    $allText = if ($ownLoader) { T 'Vanilla+ und das von diesem Installer eingerichtete BepInEx (Mod-Loader). Dateien anderer Mods bleiben erhalten.' "Vanilla+ and the BepInEx (mod loader) set up by this installer. Other mods' files are kept." } else { T 'Nur Vanilla+. BepInEx bleibt: Es gehört seit Spiel-Update 1.011 zum Spiel bzw. stammt nicht von diesem Installer.' 'Only Vanilla+. BepInEx stays: since game update 1.011 it is part of the game, or it was not installed by this installer.' }
    $f.Controls.Add((New-Text $allText 24 80 510 42 9.5 $false $ColText))
    $f.Controls.Add((New-Text (T 'NUR VANILLA+' 'VANILLA+ ONLY') 24 130 510 18 8.5 $true $ColGold))
    $f.Controls.Add((New-Text (T 'Entfernt nur diesen Mod. BepInEx und andere Mods bleiben.' 'Removes only this mod. BepInEx and other mods stay.') 24 150 510 24 9.5 $false $ColText))
    $f.Controls.Add((New-Text (T 'Deine Spielstand-Backups und Screenshots des Mods werden in jedem Fall nach Dokumente\GK2 Vanilla+ gerettet.' "The mod's save backups and screenshots are always kept in Documents\GK2 Vanilla+.") 24 184 510 36 8.5 $false $ColMuted))
    $script:removeChoice = 'cancel'
    $mk = {
        param($text, $x, $w, $value, $kind)
        $btn = New-Btn $text $kind
        $btn.SetBounds($x, 234, $w, 44)
        $btn.Tag = $value
        $btn.Add_Click({ $script:removeChoice = $this.Tag; $this.FindForm().Close() })
        $f.Controls.Add($btn)
    }
    & $mk (T 'Alles entfernen' 'Remove everything') 24 190 'all' 'danger'
    & $mk (T 'Nur Vanilla+' 'Vanilla+ only') 226 160 'mods' 'ghost'
    & $mk (T 'Abbrechen' 'Cancel') 416 120 'cancel' 'ghost'
    if ($snapshot) { Save-Snapshot $f $snapshot; return 'cancel' }
    [void]$f.ShowDialog($form)
    return $script:removeChoice
}

# Loescht eine Datei/einen Ordner auch bei Schreibschutz; gibt $false zurueck, wenn etwas uebrig bleibt
function Remove-Tree($path) {
    if (-not (Test-Path $path)) { return $true }
    try {
        Get-ChildItem -Path $path -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Attributes = 'Normal' } catch {} }
        try { (Get-Item $path -Force).Attributes = 'Normal' } catch {}
        Remove-Item $path -Recurse -Force -ErrorAction Stop
    } catch {}
    return -not (Test-Path $path)
}

# Entfernt leere Unterordner (von innen nach aussen) und zuletzt $root selbst, wenn leer; Dateien bleiben unangetastet
function Remove-EmptyDirs($root) {
    if (-not (Test-Path -LiteralPath $root)) { return }
    $dirs = @(Get-ChildItem -LiteralPath $root -Recurse -Directory -Force -ErrorAction SilentlyContinue | Sort-Object { $_.FullName.Length } -Descending)
    foreach ($d in $dirs) {
        try { if (-not (Get-ChildItem -LiteralPath $d.FullName -Force -ErrorAction SilentlyContinue | Select-Object -First 1)) { Remove-Item -LiteralPath $d.FullName -Force -ErrorAction Stop } } catch {}
    }
    try { if (-not (Get-ChildItem -LiteralPath $root -Force -ErrorAction SilentlyContinue | Select-Object -First 1)) { Remove-Item -LiteralPath $root -Force -ErrorAction Stop } } catch {}
}

$uninstall.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    # 'legacy' (Installer vor 1.7.5) zaehlt als unseres, wenn das Paket den Loader zum Vergleichen mitbringt (GitHub-ZIP)
    $loaderMode = Get-LoaderMode $dir
    $own = ($loaderMode -eq 'ours') -or ($loaderMode -eq 'legacy' -and $LoaderSrc)
    $choice = Ask-Remove $null $own
    if ($choice -eq 'cancel') { return }
    try {
        $log = New-Object System.Collections.Generic.List[string]
        $log.Add("GK2 Vanilla+ uninstall $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), mode: $choice, game folder: $dir")
        # BepInEx nur entfernen, wenn wir es installiert haben (loader-files.txt) - das des Spiels (seit 1.011) bleibt
        $loaderFiles = @()
        $loaderHashes = @{}
        if ($own -and $loaderMode -eq 'legacy') {
            $adopt = Get-LegacyLoaderFiles $LoaderSrc $dir
            $loaderFiles = @($adopt.Files); $loaderHashes = $adopt.Hashes
            $log.Add('BepInEx set up by an installer before 1.7.5: only files identical to the bundled BepInEx ' + $LoaderVersion + ' count as ours')
        } elseif ($own) {
            $loaderFiles = @(Read-LoaderMarker $dir); $loaderHashes = Read-LoaderHashes $dir
        }
        $keptLoader = $false
        if (-not $own -and (Test-LoaderPresent $dir)) {
            $keptLoader = $true
            $log.Add('kept: BepInEx (not installed by Vanilla+, e.g. part of the game since update 1.011)')
            if ($choice -eq 'all') { $choice = 'mods' }
        }
        # Spielstand-Backups und Screenshots des Mods nicht mitloeschen, sondern nach Dokumente\GK2 Vanilla+ retten
        $kept = $null
        foreach ($sub in 'Backups', 'Screenshots') {
            $src = Join-Path $dir "BepInEx\GK2VanillaPlus\$sub"
            if ((Test-Path $src) -and (Get-ChildItem -Path $src -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
                if (-not $kept) { $kept = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'GK2 Vanilla+'; New-Item -ItemType Directory -Force -Path $kept | Out-Null }
                $dst = Join-Path $kept $sub
                if (Test-Path $dst) { $dst = Join-Path $kept ($sub + ' ' + (Get-Date -Format 'yyyy-MM-dd HH-mm-ss')) }
                try { Move-Item -Path $src -Destination $dst -Force; $log.Add("kept: $sub -> $dst") } catch { $log.Add("COULD NOT KEEP $sub : $($_.Exception.Message)") }
            }
        }
        $targets = @('BepInEx\plugins\GK2Tweaks', 'BepInEx\plugins\GK2Ultrawide', 'BepInEx\GK2VanillaPlus', 'BepInEx\config\mats.gk2.tweaks.cfg', 'BepInEx\config\mats.gk2.ultrawide.cfg')
        # BepInEx.cfg nur, wenn wir es angelegt haben (steht dann in loader-files.txt) - eine vorher vorhandene Datei bleibt
        if ($choice -eq 'all') { $targets += $loaderFiles; $targets += @('BepInEx\cache', 'BepInEx\LogOutput.log') }
        $left = @()
        foreach ($p in $targets) {
            $f = Join-Path $dir $p
            if (-not (Test-Path $f)) { continue }
            if ($loaderFiles -contains $p) {
                if (-not (Test-Path $f -PathType Leaf)) { continue }   # aus loader-files.txt nur einzelne Dateien
                # seit der Installation geaendert (z. B. von Steam durch die Datei des Spiels ersetzt)? Dann bleibt sie
                if ($loaderHashes.ContainsKey($p) -and $loaderHashes[$p] -and ((Get-FileSha $f) -ne $loaderHashes[$p])) { $log.Add("kept (changed since install): $p"); continue }
            }
            if (Remove-Tree $f) { $log.Add("removed: $p") } else { $left += $p; $log.Add("LEFT OVER: $p") }
        }
        if ($choice -eq 'all') {
            # leere Ordner (core, config, plugins ...) und BepInEx selbst, wenn leer; Dateien anderer Mods bleiben
            Remove-EmptyDirs (Join-Path $dir 'BepInEx')
            if (-not (Test-Path (Join-Path $dir 'BepInEx'))) { $log.Add('removed: BepInEx (empty)') }
        } elseif ($own) {
            # "Nur Vanilla+": Liste behalten, damit "Alles entfernen" unser BepInEx spaeter noch erkennt
            try { Write-LoaderMarker $dir $loaderFiles $loaderHashes; $log.Add("kept: $LoaderMarker") } catch {}
        }
        Set-Status
        $done = if ($left.Count -eq 0) { T 'Entfernt.' 'Removed.' } else { (T "Nicht alles konnte entfernt werden (Datei in Benutzung oder gesperrt?). Übrig:`n" "Not everything could be removed (file in use or locked?). Left over:`n") + ($left -join "`n") + (T "`n`nBitte das Spiel und Steam beenden und noch einmal 'Deinstallieren' wählen." "`n`nPlease quit the game and Steam and click 'Uninstall' again.") }
        if ($keptLoader) { $done += T "`n`nBepInEx wurde nicht angefasst: Es stammt nicht von diesem Installer (seit Spiel-Update 1.011 gehört es zum Spiel)." "`n`nBepInEx was left untouched: it was not installed by this installer (since game update 1.011 it is part of the game)." }
        if ($kept) { $done += (T "`n`nDeine Spielstand-Backups und Screenshots des Mods wurden aufgehoben:`n" "`n`nThe mod's save backups and screenshots were kept:`n") + $kept }
        # Protokoll in Dokumente\GK2 Vanilla+ (der BepInEx-Ordner ist ja ggf. weg)
        try {
            $ld = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'GK2 Vanilla+'
            New-Item -ItemType Directory -Force -Path $ld | Out-Null
            [System.IO.File]::WriteAllLines((Join-Path $ld 'uninstall.log'), $log.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
        } catch {}
        [System.Windows.Forms.MessageBox]::Show($done, 'GK2 Vanilla+', 'OK', $(if ($left.Count -eq 0) { 'Information' } else { 'Warning' })) | Out-Null
    } catch {
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    }
})

# Nur fuer Entwickler-Tests: Fenster als PNG speichern statt anzeigen (GK2_SNAPSHOT=<pfad.png>)
function Save-Snapshot($frm, $path) {
    $frm.StartPosition = 'Manual'
    $frm.Location = New-Object System.Drawing.Point(40, 40)
    $frm.Show()
    for ($i = 0; $i -lt 10; $i++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
    $frm.Activate(); $frm.Refresh()
    for ($i = 0; $i -lt 10; $i++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
    # Jedes Element einzeln zeichnen und zusammensetzen (Form.DrawToBitmap ist unter Wine unvollstaendig)
    $cs = $frm.ClientSize
    $bmp = New-Object System.Drawing.Bitmap($cs.Width, $cs.Height)
    $gr = [System.Drawing.Graphics]::FromImage($bmp)
    $gr.Clear($frm.BackColor)
    $list = @($frm.Controls); [array]::Reverse($list)
    foreach ($c in $list) {
        if (-not $c.Visible -or $c.Width -le 0 -or $c.Height -le 0) { continue }
        $cb = New-Object System.Drawing.Bitmap($c.Width, $c.Height)
        $c.DrawToBitmap($cb, (New-Object System.Drawing.Rectangle(0, 0, $c.Width, $c.Height)))
        $gr.DrawImage($cb, $c.Left, $c.Top); $cb.Dispose()
    }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    ($Error | ForEach-Object { $_.ToString() + ' @ ' + $_.InvocationInfo.PositionMessage }) -join "`n`n" | Set-Content ($path + '.txt')
    $frm.Close()
}

$box.Text = Find-Game
if ($env:GK2_SNAPSHOT_DIR) { $box.Text = $env:GK2_SNAPSHOT_DIR }
if ($env:GK2_SNAPSHOT_BETA) { Select-Channel $true }
Set-Status
if ($env:GK2_SNAPSHOT_REMOVE) { $form.Show(); [void](Ask-Remove $env:GK2_SNAPSHOT_REMOVE ((Test-GameDir $box.Text) -and ((Get-LoaderMode $box.Text) -eq 'ours'))); $form.Close(); exit 0 }
if ($env:GK2_SNAPSHOT) { Save-Snapshot $form $env:GK2_SNAPSHOT; exit 0 }
[void]$form.ShowDialog()
