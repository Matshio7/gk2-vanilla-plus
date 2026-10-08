# GK2 Vanilla+ - Online-Updater (Windows PowerShell 5.1)
# Laedt die neueste Version von GitHub und installiert sie in den Spielordner.
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
function T($de, $en) { if ($De) { $de } else { $en } }
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
    $files = Get-ChildItem -Path (Join-Path $tmp 'x') -Recurse -Directory -Filter 'files' | Where-Object { Test-Path (Join-Path $_.FullName 'winhttp.dll') } | Select-Object -First 1
    if (-not $files) { throw (T 'Die heruntergeladene Datei hat ein unerwartetes Format.' 'The downloaded file has an unexpected format.') }
    Get-ChildItem -Path $files.FullName -Recurse -File -Force | ForEach-Object { try { Unblock-File -Path $_.FullName } catch {} }
    Copy-Item -Path (Join-Path $files.FullName '*') -Destination $GameDir -Recurse -Force
    Get-ChildItem -Path $files.FullName -Force -File -Filter '.*' | Copy-Item -Destination $GameDir -Force
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
    # Kanal merken (Mod-Einstellung) und installierte Release-Version (fuer Beta-Nummern)
    Set-UpdateChannel $GameDir $(if ($beta) { 'Beta' } else { 'Stable' })
    try { Set-Content -Path (Join-Path $GameDir 'BepInEx\GK2VanillaPlus\installed-tag.txt') -Value $rel.tag_name -Encoding ASCII } catch {}

    $form.Hide()
    if ($WaitPid -gt 0) {
        if ((Msg (T "GK2 Vanilla+ $latestText ist installiert. Deine Einstellungen bleiben erhalten.`n`nSpiel jetzt starten?" "GK2 Vanilla+ $latestText is installed. Your settings are kept.`n`nStart the game now?") 'YesNo') -eq 'Yes') { Start-Process 'steam://rungameid/4358690' }
    } else {
        Msg (T "GK2 Vanilla+ $latestText ist installiert. Deine Einstellungen bleiben erhalten." "GK2 Vanilla+ $latestText is installed. Your settings are kept.") | Out-Null
    }
    exit 0
}
catch {
    $form.Hide()
    $page = if ($Channel -eq 'beta') { "https://github.com/$Repo/releases" } else { "https://github.com/$Repo/releases/latest" }
    Msg ((T "Update fehlgeschlagen:`n" "Update failed:`n") + $_.Exception.Message + (T "`n`nDu kannst die neue Version auch manuell laden:`n$page" "`n`nYou can also download it manually:`n$page")) 'OK' 'Error' | Out-Null
    exit 1
}
