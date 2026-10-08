# GK2 Vanilla+ (Ultrawide + Tweaks) by McFly7 - Installer (Windows PowerShell 5.1, WinForms)
# Kopiert BepInEx und die Mods in den Spielordner von Graveyard Keeper 2.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Version = '1.2.0'
$OnlineUpdate = $true   # Nexus-Ausgabe: $false (keine Internetverbindung)
$ExeName = 'GraveyardKeeper2.exe'
$Root    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Payload = Join-Path $Root 'installer\files'
$De      = (Get-Culture).TwoLetterISOLanguageName -eq 'de'
function T($de, $en) { if ($De) { $de } else { $en } }

if (-not (Test-Path (Join-Path $Payload 'winhttp.dll'))) {
    [System.Windows.Forms.MessageBox]::Show((T "Installationsdateien nicht gefunden.`n`nBitte die ZIP-Datei zuerst komplett entpacken (Rechtsklick > Alle extrahieren) und dann 'Installieren.bat' im entpackten Ordner starten." "Installer files not found.`n`nPlease extract the whole ZIP first (right click > Extract All) and run 'Installieren.bat' from the extracted folder."), 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    exit 1
}

# --- Spielordner automatisch finden (Steam-Bibliotheken) ---
function Find-Game {
    # Aus dem Steam-Workshop gestartet? <Bibliothek>\steamapps\workshop\content\4358690\<id>\ -> <Bibliothek>\steamapps\common\Graveyard Keeper 2
    $m = [regex]::Match($Root, '^(.*\\steamapps)\\workshop\\content\\4358690\\', 'IgnoreCase')
    if ($m.Success) {
        $g = Join-Path $m.Groups[1].Value 'common\Graveyard Keeper 2'
        if (Test-Path (Join-Path $g $ExeName)) { return $g }
    }
    $steamRoots = @()
    foreach ($k in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty -Path $k -ErrorAction Stop
            if ($p.SteamPath)   { $steamRoots += ($p.SteamPath -replace '/', '\') }
            if ($p.InstallPath) { $steamRoots += $p.InstallPath }
        } catch {}
    }
    $steamRoots += "${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam"
    $libs = @()
    foreach ($r in ($steamRoots | Select-Object -Unique)) {
        $libs += $r
        $vdf = Join-Path $r 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) { $libs += ($m.Groups[1].Value -replace '\\\\', '\') }
        }
    }
    foreach ($l in ($libs | Select-Object -Unique)) {
        $g = Join-Path $l 'steamapps\common\Graveyard Keeper 2'
        if (Test-Path (Join-Path $g $ExeName)) { return $g }
    }
    return ''
}

function Test-GameDir($dir) { return ($dir -and (Test-Path (Join-Path $dir $ExeName))) }

function Test-GameRunning {
    if (Get-Process -Name 'GraveyardKeeper2' -ErrorAction SilentlyContinue) {
        [System.Windows.Forms.MessageBox]::Show((T "Graveyard Keeper 2 läuft noch. Bitte das Spiel zuerst beenden." "Graveyard Keeper 2 is still running. Please quit the game first."), 'GK2 Vanilla+', 'OK', 'Warning') | Out-Null
        return $true
    }
    return $false
}

function New-Btn($text, $x, $w, $r, $g, $b) {
    $btn = New-Object System.Windows.Forms.Button
    $btn.Text = $text
    $btn.SetBounds($x, 290, $w, 40)
    $btn.BackColor = [System.Drawing.Color]::FromArgb($r, $g, $b)
    $btn.ForeColor = [System.Drawing.Color]::White
    $btn.FlatStyle = 'Flat'
    return $btn
}

# --- Fenster ---
$form = New-Object System.Windows.Forms.Form
$form.Text = "GK2 Vanilla+ $Version  ·  by McFly7"
$form.ClientSize = New-Object System.Drawing.Size(610, 395)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
$form.BackColor = [System.Drawing.Color]::FromArgb(38, 40, 48)
$form.ForeColor = [System.Drawing.Color]::FromArgb(236, 222, 190)

$title = New-Object System.Windows.Forms.Label
$title.Text = 'GK2 Vanilla+  ·  Ultrawide, Performance & QoL'
$title.Font = New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
$title.ForeColor = [System.Drawing.Color]::FromArgb(255, 214, 140)
$title.SetBounds(20, 15, 580, 30)
$form.Controls.Add($title)

$info = New-Object System.Windows.Forms.Label
$info.Text = T "Wähle den Spielordner (dort liegt GraveyardKeeper2.exe) und klicke auf Installieren.`nIm Spiel öffnet F9 das Mod-Menü." "Choose the game folder (it contains GraveyardKeeper2.exe) and click Install.`nIn game, F9 opens the mod menu."
$info.SetBounds(20, 52, 580, 44)
$form.Controls.Add($info)

$box = New-Object System.Windows.Forms.TextBox
$box.SetBounds(20, 105, 450, 28)
$form.Controls.Add($box)

$browse = New-Btn (T 'Durchsuchen …' 'Browse …') 480 110 70 72 82
$browse.SetBounds(480, 103, 110, 30)
$form.Controls.Add($browse)

$status = New-Object System.Windows.Forms.Label
$status.SetBounds(20, 143, 580, 56)
$form.Controls.Add($status)

# Welche Version? Stabil = dieses Paket (offline), Beta = neueste Vorabversion von GitHub
$rbStable = New-Object System.Windows.Forms.RadioButton
$rbStable.Text = T 'Stabil (empfohlen) – getestete Version' 'Stable (recommended) – tested version'
$rbStable.SetBounds(20, 205, 290, 26)
$rbStable.Checked = $true
$rbBeta = New-Object System.Windows.Forms.RadioButton
$rbBeta.Text = T 'Beta – neue Funktionen testen' 'Beta – try new features'
$rbBeta.SetBounds(320, 205, 270, 26)
$rbHint = New-Object System.Windows.Forms.Label
$rbHint.ForeColor = [System.Drawing.Color]::FromArgb(180, 170, 150)
$rbHint.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$rbHint.Text = T "Stabil kommt direkt aus diesem Paket (ohne Internet). Beta lädt die neueste Vorabversion von GitHub – sie kann Fehler enthalten.`nDen Kanal kannst du später im Mod-Menü (F9) unter 'Update-Kanal' ändern." "Stable is installed straight from this package (no internet needed). Beta downloads the newest pre-release from GitHub – it may contain bugs.`nYou can change the channel later in the mod menu (F9) under 'Update channel'."
$rbHint.SetBounds(20, 233, 580, 48)
if ($OnlineUpdate) { $form.Controls.AddRange(@($rbStable, $rbBeta, $rbHint)) }

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

function Set-Status {
    if (Test-GameDir $box.Text) {
        $have = Get-InstalledVersion $box.Text
        $pkg  = [version]$Version
        $status.ForeColor = [System.Drawing.Color]::FromArgb(150, 220, 140)
        if (Test-Beta) {
            $install.Text = T 'Beta herunterladen && installieren' 'Download && install beta'
            $haveText = if ($have -eq $null) { T 'noch nicht installiert' 'not installed yet' } else { "$have" }
            $status.ForeColor = [System.Drawing.Color]::FromArgb(240, 200, 120)
            $status.Text = T "Beta-Version (Vorabversion): wird aus dem Internet geladen (installiert: $haveText). Sie kann Fehler enthalten – bitte vorher Spielstand sichern. Einstellungen bleiben erhalten." "Beta (pre-release): downloaded from the internet (installed: $haveText). It may contain bugs – please back up your saves first. Your settings are kept."
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
            $status.ForeColor = [System.Drawing.Color]::FromArgb(240, 200, 120)
            $status.Text = T "Installiert ist eine neuere Version ($have). Dieses Paket ($Version) ist älter." "A newer version ($have) is installed. This package ($Version) is older."
        }
    } else {
        $install.Text = T 'Installieren' 'Install'
        $status.ForeColor = [System.Drawing.Color]::FromArgb(240, 150, 120)
        $status.Text = T 'In diesem Ordner liegt keine GraveyardKeeper2.exe. Bitte den Spielordner auswählen (Steam: Rechtsklick auf das Spiel > Verwalten > Lokale Dateien durchsuchen).' 'No GraveyardKeeper2.exe in this folder. Please select the game folder (Steam: right click the game > Manage > Browse local files).'
    }
}
$box.Add_TextChanged({ Set-Status })
$rbStable.Add_CheckedChanged({ Set-Status })
$rbBeta.Add_CheckedChanged({ Set-Status })

$browse.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = T 'Spielordner von Graveyard Keeper 2 auswählen' 'Select the Graveyard Keeper 2 game folder'
    if ($box.Text -and (Test-Path $box.Text)) { $dlg.SelectedPath = $box.Text }
    if ($dlg.ShowDialog() -eq 'OK') { $box.Text = $dlg.SelectedPath }
})

$install   = New-Btn (T 'Installieren' 'Install') 20 200 150 36 36
$uninstall = New-Btn (T 'Deinstallieren' 'Uninstall') 230 160 70 72 82
$close     = New-Btn (T 'Schließen' 'Close') 470 120 70 72 82
$online    = New-Btn (T 'Online nach Updates suchen' 'Check online for updates') 20 370 70 72 82
$online.SetBounds(20, 342, 370, 36)
$credit = New-Object System.Windows.Forms.LinkLabel
$credit.Text = 'github.com/Matshio7/gk2-vanilla-plus'
$credit.LinkColor = [System.Drawing.Color]::FromArgb(150, 200, 255)
$credit.SetBounds(400, 351, 200, 22)
$credit.TextAlign = 'MiddleRight'
$credit.Add_LinkClicked({ Start-Process 'https://github.com/Matshio7/gk2-vanilla-plus' })
$form.Controls.AddRange(@($install, $uninstall, $close, $credit))
if ($OnlineUpdate) { $form.Controls.Add($online) }
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
        Get-ChildItem -Path $Payload -Recurse -File -Force | ForEach-Object { try { Unblock-File -Path $_.FullName } catch {} }
        Copy-Item -Path (Join-Path $Payload '*') -Destination $dir -Recurse -Force
        Get-ChildItem -Path $Payload -Force -File -Filter '.*' | Copy-Item -Destination $dir -Force
        # Stabil gewaehlt: Kanal merken, Beta-Markierung entfernen
        if ($OnlineUpdate) { Set-UpdateChannel $dir 'Stable' }
        $mark = Join-Path $dir 'BepInEx\GK2VanillaPlus\installed-tag.txt'
        if (Test-Path $mark) { Remove-Item $mark -Force -ErrorAction SilentlyContinue }
        $status.ForeColor = [System.Drawing.Color]::FromArgb(150, 220, 140)
        $msg = T "Fertig! Version $Version ist installiert.`n`nSpiel ganz normal über Steam starten. Im Spiel öffnet F9 das Mod-Menü, F10 die FPS-Anzeige." "Done! Version $Version is installed.`n`nStart the game normally via Steam. In game, F9 opens the mod menu, F10 the FPS display."
        [System.Windows.Forms.MessageBox]::Show($msg, 'GK2 Vanilla+', 'OK', 'Information') | Out-Null
        Set-Status
    } catch {
        [System.Windows.Forms.MessageBox]::Show((T "Fehler beim Kopieren:`n" "Copy failed:`n") + $_.Exception.Message + (T "`n`nTipp: Installieren.bat per Rechtsklick 'Als Administrator ausführen', falls das Spiel unter 'Programme' liegt." "`n`nTip: right click Installieren.bat > 'Run as administrator' if the game is inside 'Program Files'."), 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    }
})

# Auswahl beim Deinstallieren: 'all' (alles entfernen), 'mods' (nur Vanilla+) oder 'cancel'
function Ask-Remove {
    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'GK2 Vanilla+'
    $f.ClientSize = New-Object System.Drawing.Size(520, 250)
    $f.StartPosition = 'CenterParent'
    $f.FormBorderStyle = 'FixedDialog'
    $f.MaximizeBox = $false; $f.MinimizeBox = $false
    $f.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $f.BackColor = [System.Drawing.Color]::FromArgb(38, 40, 48)
    $f.ForeColor = [System.Drawing.Color]::FromArgb(236, 222, 190)
    $l = New-Object System.Windows.Forms.Label
    $l.Text = T "Was soll entfernt werden?`n`nAlles entfernen: Vanilla+ und BepInEx (der Mod-Loader) – das Spiel ist danach wieder original. Ohne andere Mods ist das die richtige Wahl.`n`nNur Vanilla+ entfernen: BepInEx und andere Mods bleiben." "What should be removed?`n`nRemove everything: Vanilla+ and BepInEx (the mod loader) – the game is back to original. Without other mods this is the right choice.`n`nRemove Vanilla+ only: BepInEx and other mods stay."
    $l.SetBounds(20, 15, 480, 150)
    $f.Controls.Add($l)
    $script:removeChoice = 'cancel'
    $mk = {
        param($text, $x, $w, $value, $r, $g, $bl)
        $btn = New-Object System.Windows.Forms.Button
        $btn.Text = $text
        $btn.SetBounds($x, 185, $w, 44)
        $btn.BackColor = [System.Drawing.Color]::FromArgb($r, $g, $bl)
        $btn.ForeColor = [System.Drawing.Color]::White
        $btn.FlatStyle = 'Flat'
        $btn.Tag = $value
        $btn.Add_Click({ $script:removeChoice = $this.Tag; $this.FindForm().Close() })
        $f.Controls.Add($btn)
    }
    & $mk (T 'Alles entfernen' 'Remove everything') 20 170 'all' 150 60 60
    & $mk (T 'Nur Vanilla+' 'Vanilla+ only') 200 150 'mods' 70 72 82
    & $mk (T 'Abbrechen' 'Cancel') 360 140 'cancel' 70 72 82
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

$uninstall.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    $choice = Ask-Remove
    if ($choice -eq 'cancel') { return }
    try {
        $log = New-Object System.Collections.Generic.List[string]
        $log.Add("GK2 Vanilla+ uninstall $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), mode: $choice, game folder: $dir")
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
        if ($choice -eq 'all') { $targets += @('BepInEx', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') }
        $left = @()
        foreach ($p in $targets) {
            $f = Join-Path $dir $p
            if (-not (Test-Path $f)) { continue }
            if (Remove-Tree $f) { $log.Add("removed: $p") } else { $left += $p; $log.Add("LEFT OVER: $p") }
        }
        Set-Status
        $done = if ($left.Count -eq 0) { T 'Entfernt.' 'Removed.' } else { (T "Nicht alles konnte entfernt werden (Datei in Benutzung oder gesperrt?). Übrig:`n" "Not everything could be removed (file in use or locked?). Left over:`n") + ($left -join "`n") + (T "`n`nBitte das Spiel und Steam beenden und noch einmal 'Deinstallieren' wählen." "`n`nPlease quit the game and Steam and click 'Uninstall' again.") }
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

$box.Text = Find-Game
Set-Status
[void]$form.ShowDialog()
