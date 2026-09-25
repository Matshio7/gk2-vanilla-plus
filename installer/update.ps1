# GK2 Vanilla+ - Online-Updater (Windows PowerShell 5.1)
# Laedt die neueste Version von GitHub und installiert sie in den Spielordner.
#   -GameDir  Spielordner (dort liegt GraveyardKeeper2.exe)
#   -WaitPid  wartet, bis dieser Prozess (das Spiel) beendet ist  (Aufruf aus dem Mod-Menue)
#   -Ask      erst fragen, ob installiert werden soll                (Aufruf aus dem Installer)
param([string]$GameDir, [int]$WaitPid = 0, [switch]$Ask)
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
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers @{ 'User-Agent' = 'GK2VanillaPlus-Updater' } -UseBasicParsing
    $latest = [version](($rel.tag_name -replace '^v', ''))
    $have = Get-InstalledVersion $GameDir
    $asset = $rel.assets | Where-Object { $_.name -like 'GK2-VanillaPlus-*.zip' } | Select-Object -First 1
    if (-not $asset) { throw (T 'Im Release wurde keine ZIP-Datei gefunden.' 'No ZIP file found in the release.') }

    if ($have -ne $null -and $have -ge $latest) {
        $form.Hide()
        Msg (T "Du hast bereits die neueste Version ($have)." "You already have the latest version ($have).") | Out-Null
        exit 0
    }
    if ($Ask) {
        $form.Hide()
        $haveText = if ($have) { "$have" } else { T 'keine' 'none' }
        if ((Msg (T "Version $latest ist verfügbar (installiert: $haveText).`n`nJetzt herunterladen und installieren?" "Version $latest is available (installed: $haveText).`n`nDownload and install now?") 'YesNo' 'Question') -ne 'Yes') { exit 0 }
    }

    Status (T "Lade Version $latest herunter …" "Downloading version $latest …")
    $tmp = Join-Path $env:TEMP ('gk2vp_' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $zip = Join-Path $tmp $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing -Headers @{ 'User-Agent' = 'GK2VanillaPlus-Updater' }

    Status (T "Installiere Version $latest …" "Installing version $latest …")
    Expand-Archive -Path $zip -DestinationPath (Join-Path $tmp 'x') -Force
    $files = Get-ChildItem -Path (Join-Path $tmp 'x') -Recurse -Directory -Filter 'files' | Where-Object { Test-Path (Join-Path $_.FullName 'winhttp.dll') } | Select-Object -First 1
    if (-not $files) { throw (T 'Die heruntergeladene Datei hat ein unerwartetes Format.' 'The downloaded file has an unexpected format.') }
    Get-ChildItem -Path $files.FullName -Recurse -File -Force | ForEach-Object { try { Unblock-File -Path $_.FullName } catch {} }
    Copy-Item -Path (Join-Path $files.FullName '*') -Destination $GameDir -Recurse -Force
    Get-ChildItem -Path $files.FullName -Force -File -Filter '.*' | Copy-Item -Destination $GameDir -Force
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue

    $form.Hide()
    if ($WaitPid -gt 0) {
        if ((Msg (T "GK2 Vanilla+ $latest ist installiert. Deine Einstellungen bleiben erhalten.`n`nSpiel jetzt starten?" "GK2 Vanilla+ $latest is installed. Your settings are kept.`n`nStart the game now?") 'YesNo') -eq 'Yes') { Start-Process 'steam://rungameid/4358690' }
    } else {
        Msg (T "GK2 Vanilla+ $latest ist installiert. Deine Einstellungen bleiben erhalten." "GK2 Vanilla+ $latest is installed. Your settings are kept.") | Out-Null
    }
    exit 0
}
catch {
    $form.Hide()
    Msg ((T "Update fehlgeschlagen:`n" "Update failed:`n") + $_.Exception.Message + (T "`n`nDu kannst die neue Version auch manuell laden:`nhttps://github.com/$Repo/releases/latest" "`n`nYou can also download it manually:`nhttps://github.com/$Repo/releases/latest")) 'OK' 'Error' | Out-Null
    exit 1
}
