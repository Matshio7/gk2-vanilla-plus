# GK2 Vanilla+ - Online-Updater (Windows PowerShell 5.1)
# Laedt die neueste Version von GitHub und installiert sie in den Spielordner (BepInEx des Spiels bleibt unveraendert).
#   -GameDir  Spielordner (dort liegt GraveyardKeeper2.exe)
#   -WaitPid  wartet, bis dieser Prozess (das Spiel) beendet ist  (Aufruf aus dem Mod-Menue)
#   -Ask      erst fragen, ob installiert werden soll                (Aufruf aus dem Installer)
#   -Channel  'stable' (Standard) oder 'beta' (Vorabversion, GitHub-Pre-release)
param([string]$GameDir, [int]$WaitPid = 0, [switch]$Ask, [string]$Channel = 'stable')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Repo  = 'Matshio7/gk2-vanilla-plus'
$Title = 'GK2 Vanilla+'
$De    = (Get-Culture).TwoLetterISOLanguageName -eq 'de'
function T($textDe, $textEn) { if ($script:De) { $textDe } else { $textEn } }   # Parameter nicht $de nennen: PowerShell unterscheidet keine Gross-/Kleinschreibung
function Msg($text, $buttons = 'OK', $icon = 'Information') { [System.Windows.Forms.MessageBox]::Show($text, $Title, $buttons, $icon) }

function Get-InstalledVersion($dir) {
    $f = Join-Path $dir 'BepInEx\plugins\GK2Tweaks\GK2Tweaks.dll'
    if (-not (Test-Path $f)) { return $null }
    try { $v = [version](Get-Item $f).VersionInfo.FileVersion; return [version]('{0}.{1}.{2}' -f $v.Major, $v.Minor, [Math]::Max(0, $v.Build)) } catch { return [version]'0.0.0' }
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

# Release-Schluessel: 4. Stelle = 65534 fuer stabile Versionen, Beta-Nummer fuer Betas (1.8.0-beta.2 < 1.8.0)
function Get-TagKey($tag) {
    $m = [regex]::Match([string]$tag, '^v?(\d+(?:\.\d+){1,2})(?:-beta\.?(\d+))?$')
    if (-not $m.Success) { return $null }
    $v = [version]$m.Groups[1].Value
    $rev = if ($m.Groups[2].Success) { [int]$m.Groups[2].Value } else { 65534 }
    return [version]('{0}.{1}.{2}.{3}' -f $v.Major, $v.Minor, [Math]::Max(0, $v.Build), $rev)
}
function Get-InstalledKey($dir) {
    $v = Get-InstalledVersion $dir
    if ($v -eq $null) { return $null }
    $key = [version]('{0}.{1}.{2}.65534' -f $v.Major, $v.Minor, $v.Build)
    $mark = Join-Path $dir 'BepInEx\GK2VanillaPlus\installed-tag.txt'
    if (Test-Path $mark) {
        $k = Get-TagKey ((Get-Content $mark -Raw).Trim())
        if ($k -ne $null -and $k.Major -eq $key.Major -and $k.Minor -eq $key.Minor -and $k.Build -eq $key.Build) { $key = $k }
    }
    return $key
}
function Show-Key($k) { if ($k -eq $null) { return (T 'keine' 'none') } elseif ($k.Revision -eq 65534) { return ('{0}.{1}.{2}' -f $k.Major, $k.Minor, $k.Build) } else { return ('{0}.{1}.{2} Beta {3}' -f $k.Major, $k.Minor, $k.Build, $k.Revision) } }

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

# kleines Statusfenster
$form = New-Object System.Windows.Forms.Form
$form.Text = $Title
$form.ClientSize = New-Object System.Drawing.Size(460, 90)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.ControlBox = $false
$form.TopMost = $true
$form.BackColor = [System.Drawing.Color]::FromArgb(38, 40, 48)
$form.ForeColor = [System.Drawing.Color]::FromArgb(236, 222, 190)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
$label = New-Object System.Windows.Forms.Label
$label.SetBounds(20, 20, 420, 50)
$form.Controls.Add($label)
function Status($text) { $label.Text = $text; if (-not $form.Visible) { $form.Show() }; [System.Windows.Forms.Application]::DoEvents() }

try {
    if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir 'GraveyardKeeper2.exe'))) { throw (T "Spielordner nicht gefunden: $GameDir" "Game folder not found: $GameDir") }

    if ($WaitPid -gt 0) {
        Status (T 'Warte, bis das Spiel beendet ist …' 'Waiting for the game to close …')
        try { Wait-Process -Id $WaitPid -Timeout 120 -ErrorAction SilentlyContinue } catch {}
    }
    for ($i = 0; $i -lt 60 -and (Get-Process -Name 'GraveyardKeeper2' -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Seconds 1; [System.Windows.Forms.Application]::DoEvents() }
    if (Get-Process -Name 'GraveyardKeeper2' -ErrorAction SilentlyContinue) { throw (T 'Das Spiel läuft noch. Bitte beenden und erneut versuchen.' 'The game is still running. Please quit it and try again.') }

    Status (T 'Suche nach der neuesten Version …' 'Looking for the latest version …')
    $beta = ($Channel -eq 'beta')
    $headers = @{ 'User-Agent' = 'GK2VanillaPlus-Updater' }
    if ($beta) {
        $list = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases?per_page=20" -Headers $headers -UseBasicParsing
        $rel = $null; $latest = $null
        foreach ($r in $list) {
            if ($r.draft) { continue }
            $k = Get-TagKey $r.tag_name
            if ($k -ne $null -and ($latest -eq $null -or $k -gt $latest)) { $latest = $k; $rel = $r }
        }
        if (-not $rel) { throw (T 'Keine Version gefunden.' 'No version found.') }
    } else {
        $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers $headers -UseBasicParsing
        $latest = Get-TagKey $rel.tag_name
        if ($latest -eq $null) { throw (T "Unbekanntes Versions-Tag: $($rel.tag_name)" "Unknown version tag: $($rel.tag_name)") }
    }
    $have = Get-InstalledKey $GameDir
    $asset = $rel.assets | Where-Object { $_.name -like 'GK2-VanillaPlus-*.zip' } | Select-Object -First 1
    if (-not $asset) { throw (T 'Im Release wurde keine ZIP-Datei gefunden.' 'No ZIP file found in the release.') }
    $latestText = Show-Key $latest

    if ($have -ne $null -and $have -ge $latest) {
        $form.Hide()
        Msg (T "Du hast bereits die neueste Version ($(Show-Key $have))." "You already have the latest version ($(Show-Key $have)).") | Out-Null
        exit 0
    }
    if ($Ask) {
        $form.Hide()
        $q = T "Version $latestText ist verfügbar (installiert: $(Show-Key $have)).`n`nJetzt herunterladen und installieren?" "Version $latestText is available (installed: $(Show-Key $have)).`n`nDownload and install now?"
        if ($beta) { $q = (T "BETA-Version $latestText (installiert: $(Show-Key $have)).`n`nVorabversion zum Testen – sie kann Fehler enthalten. Bitte sichere vorher deinen Spielstand. Fehler bitte melden.`n`nJetzt herunterladen und installieren?" "BETA version $latestText (installed: $(Show-Key $have)).`n`nA pre-release for testing – it may contain bugs. Please back up your saves first. Reports are welcome.`n`nDownload and install now?") }
        if ((Msg $q 'YesNo' 'Question') -ne 'Yes') { exit 2 }
    }

    Status (T "Lade Version $latestText herunter …" "Downloading version $latestText …")
    $tmp = Join-Path $env:TEMP ('gk2vp_' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $zip = Join-Path $tmp $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing -Headers @{ 'User-Agent' = 'GK2VanillaPlus-Updater' }

    Status (T "Installiere Version $latestText …" "Installing version $latestText …")
    Expand-Archive -Path $zip -DestinationPath (Join-Path $tmp 'x') -Force
    # Paket finden: Ordner mit BepInEx\plugins\GK2Tweaks\GK2Tweaks.dll (GitHub-ZIP: installer\files, Workshop-Aufbau: Paketwurzel)
    $dll = Get-ChildItem -LiteralPath (Join-Path $tmp 'x') -Recurse -File -Filter 'GK2Tweaks.dll' | Where-Object { $_.Directory.Name -eq 'GK2Tweaks' -and $_.Directory.Parent.Name -eq 'plugins' -and $_.Directory.Parent.Parent.Name -eq 'BepInEx' } | Select-Object -First 1
    if (-not $dll) { throw (T 'Die heruntergeladene Datei hat ein unerwartetes Format.' 'The downloaded file has an unexpected format.') }
    $payload = $dll.Directory.Parent.Parent.Parent.FullName
    # Mod-Loader im Paket: installer\loader oder installer\files (GitHub-ZIP); fehlt er, wird nur der Mod installiert
    $loaderSrc = $null
    if ((Split-Path -Leaf $payload) -eq 'files') {
        foreach ($c in @((Join-Path (Split-Path -Parent $payload) 'loader'), $payload)) {
            if (Test-Path -LiteralPath (Join-Path $c 'winhttp.dll')) { $loaderSrc = $c; break }
        }
    }
    # Mod-Dateien immer; BepInEx nur, wenn im Spielordner noch keins liegt (das des Spiels bleibt unveraendert)
    $loaderMode = Install-Payload $payload $loaderSrc $GameDir
    if ($loaderMode -eq 'missing') { throw (T 'Die heruntergeladene Version enthält kein BepInEx (Mod-Loader), und im Spielordner ist keins. Seit Spiel-Update 1.011 bringt das Spiel BepInEx selbst mit: bitte das Spiel über Steam aktualisieren.' 'The downloaded version contains no BepInEx (mod loader) and the game folder has none. Since game update 1.011 the game ships BepInEx: please update the game via Steam.') }
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
    # Kanal merken (Mod-Einstellung) und installierte Release-Version (fuer Beta-Nummern)
    Set-UpdateChannel $GameDir $(if ($beta) { 'Beta' } else { 'Stable' })
    try { Set-Content -Path (Join-Path $GameDir 'BepInEx\GK2VanillaPlus\installed-tag.txt') -Value $rel.tag_name -Encoding ASCII } catch {}

    $form.Hide()
    $note = if ($loaderMode -eq 'foreign') { T "`n`nDas vorhandene BepInEx (z. B. das des Spiels) wurde nicht verändert." "`n`nThe existing BepInEx (e.g. the game's own) was left unchanged." } else { '' }
    if ($loaderMode -eq 'partial') { $note = T "`n`nACHTUNG: Im Spielordner fehlt BepInEx (nur eine Mod-Loader-Datei ohne BepInEx\core). Vanilla+ lädt erst, wenn BepInEx vollständig ist – in Steam die Spieldateien prüfen lassen." "`n`nWARNING: BepInEx is missing in the game folder (only a mod loader file without BepInEx\core). Vanilla+ only loads once BepInEx is complete – let Steam verify the game files." }
    if ($WaitPid -gt 0) {
        if ((Msg ((T "GK2 Vanilla+ $latestText ist installiert. Deine Einstellungen bleiben erhalten." "GK2 Vanilla+ $latestText is installed. Your settings are kept.") + $note + (T "`n`nSpiel jetzt starten?" "`n`nStart the game now?")) 'YesNo') -eq 'Yes') { Start-Process 'steam://rungameid/4358690' }
    } else {
        Msg ((T "GK2 Vanilla+ $latestText ist installiert. Deine Einstellungen bleiben erhalten." "GK2 Vanilla+ $latestText is installed. Your settings are kept.") + $note) | Out-Null
    }
    exit 0
}
catch {
    $form.Hide()
    $page = if ($Channel -eq 'beta') { "https://github.com/$Repo/releases" } else { "https://github.com/$Repo/releases/latest" }
    Msg ((T "Update fehlgeschlagen:`n" "Update failed:`n") + $_.Exception.Message + (T "`n`nDu kannst die neue Version auch manuell laden:`n$page" "`n`nYou can also download it manually:`n$page")) 'OK' 'Error' | Out-Null
    exit 1
}
