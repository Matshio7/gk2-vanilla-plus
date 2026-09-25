# GK2 Vanilla+ (Ultrawide + Tweaks) by McFly7 - Installer (Windows PowerShell 5.1, WinForms)
# Kopiert BepInEx und die Mods in den Spielordner von Graveyard Keeper 2.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Version = '1.2.0'
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
    $btn.SetBounds($x, 220, $w, 40)
    $btn.BackColor = [System.Drawing.Color]::FromArgb($r, $g, $b)
    $btn.ForeColor = [System.Drawing.Color]::White
    $btn.FlatStyle = 'Flat'
    return $btn
}

# --- Fenster ---
$form = New-Object System.Windows.Forms.Form
$form.Text = "GK2 Vanilla+ $Version  ·  by McFly7"
$form.ClientSize = New-Object System.Drawing.Size(610, 330)
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
$status.SetBounds(20, 145, 580, 60)
$form.Controls.Add($status)

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

function Set-Status {
    if (Test-GameDir $box.Text) {
        $have = Get-InstalledVersion $box.Text
        $pkg  = [version]$Version
        $status.ForeColor = [System.Drawing.Color]::FromArgb(150, 220, 140)
        if ($have -eq $null) {
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
$online.SetBounds(20, 272, 370, 36)
$credit = New-Object System.Windows.Forms.LinkLabel
$credit.Text = 'github.com/Matshio7/gk2-vanilla-plus'
$credit.LinkColor = [System.Drawing.Color]::FromArgb(150, 200, 255)
$credit.SetBounds(400, 281, 200, 22)
$credit.TextAlign = 'MiddleRight'
$credit.Add_LinkClicked({ Start-Process 'https://github.com/Matshio7/gk2-vanilla-plus' })
$form.Controls.AddRange(@($install, $uninstall, $close, $online, $credit))
$online.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    & (Join-Path $Payload 'BepInEx\GK2VanillaPlus\update.ps1') -GameDir $dir -Ask
    Set-Status
})
$close.Add_Click({ $form.Close() })

$install.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    try {
        Get-ChildItem -Path $Payload -Recurse -File -Force | ForEach-Object { try { Unblock-File -Path $_.FullName } catch {} }
        Copy-Item -Path (Join-Path $Payload '*') -Destination $dir -Recurse -Force
        Get-ChildItem -Path $Payload -Force -File -Filter '.*' | Copy-Item -Destination $dir -Force
        $status.ForeColor = [System.Drawing.Color]::FromArgb(150, 220, 140)
        $msg = T "Fertig! Version $Version ist installiert.`n`nSpiel ganz normal über Steam starten. Im Spiel öffnet F9 das Mod-Menü, F10 die FPS-Anzeige." "Done! Version $Version is installed.`n`nStart the game normally via Steam. In game, F9 opens the mod menu, F10 the FPS display."
        [System.Windows.Forms.MessageBox]::Show($msg, 'GK2 Vanilla+', 'OK', 'Information') | Out-Null
        Set-Status
    } catch {
        [System.Windows.Forms.MessageBox]::Show((T "Fehler beim Kopieren:`n" "Copy failed:`n") + $_.Exception.Message + (T "`n`nTipp: Installieren.bat per Rechtsklick 'Als Administrator ausführen', falls das Spiel unter 'Programme' liegt." "`n`nTip: right click Installieren.bat > 'Run as administrator' if the game is inside 'Program Files'."), 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    }
})

$uninstall.Add_Click({
    $dir = $box.Text
    if (-not (Test-GameDir $dir)) { Set-Status; return }
    if (Test-GameRunning) { return }
    $all = [System.Windows.Forms.MessageBox]::Show((T "Mods entfernen.`n`nSoll auch BepInEx (der Mod-Loader) komplett entfernt werden?`n`nJa = alles entfernen, das Spiel ist danach wieder original.`nNein = nur diese beiden Mods entfernen." "Remove the mods.`n`nAlso remove BepInEx (the mod loader) completely?`n`nYes = remove everything, the game is back to original.`nNo = remove only these two mods."), 'GK2 Vanilla+', 'YesNoCancel', 'Question')
    if ($all -eq 'Cancel') { return }
    try {
        foreach ($p in 'BepInEx\plugins\GK2Tweaks', 'BepInEx\plugins\GK2Ultrawide', 'BepInEx\GK2VanillaPlus', 'BepInEx\config\mats.gk2.tweaks.cfg', 'BepInEx\config\mats.gk2.ultrawide.cfg') {
            $f = Join-Path $dir $p; if (Test-Path $f) { Remove-Item $f -Recurse -Force }
        }
        if ($all -eq 'Yes') {
            foreach ($p in 'BepInEx', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') {
                $f = Join-Path $dir $p; if (Test-Path $f) { Remove-Item $f -Recurse -Force }
            }
        }
        Set-Status
        [System.Windows.Forms.MessageBox]::Show((T 'Entfernt.' 'Removed.'), 'GK2 Vanilla+', 'OK', 'Information') | Out-Null
    } catch {
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'GK2 Vanilla+', 'OK', 'Error') | Out-Null
    }
})

$box.Text = Find-Game
Set-Status
[void]$form.ShowDialog()
