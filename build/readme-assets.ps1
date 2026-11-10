# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Karl BULEON
# Additional terms under GPLv3 section 7: see the License section of README.md.

<#
.SYNOPSIS
    Regenerates the images of the README, in every language: its screenshots and its animated tour.

.DESCRIPTION
    Builds the application and the tour's tool (tools/FolderSwitch.DemoGif), both in Release, then for each language:
    draws the application's snapshots (FolderSwitch.exe --snapshot, in that language) into an emptied
    artifacts/readme-assets/<language>, copies five of them, at 144 DPI, into assets/screenshots/<language> under fixed
    names (board.png, edit-mode.png, icon-picker.png, add-menu.png, settings-appearance.png), and draws the tour into
    assets/gif/folderswitch-tour-<language>.gif. The tour's scenes, and the middle frame of each crossfade, are also
    written to artifacts/readme-assets/frames-<language> for review.

    The snapshots of French, the application's reference language, have no language suffix; the others end with
    -<language>. Works in Windows PowerShell 5.1 and in PowerShell 7.

.PARAMETER Language
    Optional. The languages to draw, among en, fr, de, es, it, nl, pl, pt-BR and pt-PT (for instance -Language de,pl).
    All of them when omitted.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/readme-assets.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/readme-assets.ps1 -Language de,pl
#>
[CmdletBinding()]
param(
    [string[]] $Language
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The languages of the application (src/FolderSwitch.App/Strings/<language>.json).
$all = @('en', 'fr', 'de', 'es', 'it', 'nl', 'pl', 'pt-BR', 'pt-PT')
if (-not $Language) {
    $Language = $all
}

# "-Language de,pl" from cmd.exe arrives as one string.
$Language = @($Language | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($code in $Language) {
    if ($all -notcontains $code) {
        throw "Unknown language '$code': expected one of $($all -join ', ')."
    }
}

# The README's screenshots and the snapshot each one is copied from (<theme>-<variant>[-<kind>]-144dpi).
$screenshots = [ordered]@{
    'board.png'               = 'fluent-light-144dpi'
    'edit-mode.png'           = 'cards-dark-customize-144dpi'
    'icon-picker.png'         = 'fluent-light-iconpicker-144dpi'
    'add-menu.png'            = 'fluent-light-addmenu-browser-144dpi'
    'settings-appearance.png' = 'fluent-light-settings-appearance-144dpi'
}

# Everything below is relative to the repository root (this script is in <root>/build).
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    foreach ($project in 'src/FolderSwitch.App', 'tools/FolderSwitch.DemoGif') {
        Write-Host "==> Building $project"
        dotnet build $project -c Release -nologo -v quiet
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build $project failed (exit code $LASTEXITCODE)."
        }
    }

    $app = Join-Path $root 'src/FolderSwitch.App/bin/Release/net10.0-windows/FolderSwitch.exe'
    $tool = Join-Path $root 'tools/FolderSwitch.DemoGif/bin/Release/net10.0-windows/FolderSwitch.DemoGif.exe'
    foreach ($path in $app, $tool) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Not found after the build: $path."
        }
    }

    $gifDir = Join-Path $root 'assets/gif'
    New-Item -ItemType Directory -Force $gifDir | Out-Null

    foreach ($code in $Language) {
        $suffix = if ($code -eq 'fr') { '' } else { "-$code" }
        $snapshotDir = Join-Path $root "artifacts/readme-assets/$code"
        $framesDir = Join-Path $root "artifacts/readme-assets/frames-$code"
        $screenshotDir = Join-Path $root "assets/screenshots/$code"

        Write-Host "==> $code`: snapshots"
        if (Test-Path -LiteralPath $snapshotDir) {
            Remove-Item -Recurse -Force -LiteralPath $snapshotDir
        }
        New-Item -ItemType Directory -Force $snapshotDir | Out-Null

        # FolderSwitch.exe is a Windows application: Start-Process waits for it, and gives its exit code.
        $process = Start-Process -FilePath $app -ArgumentList "--snapshot `"$snapshotDir`" --snapshot-language $code" -NoNewWindow -PassThru
        $null = $process.Handle
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "The snapshots of '$code' failed (exit code $($process.ExitCode))."
        }

        Write-Host "==> $code`: screenshots"
        New-Item -ItemType Directory -Force $screenshotDir | Out-Null
        foreach ($name in $screenshots.Keys) {
            $source = Join-Path $snapshotDir "$($screenshots[$name])$suffix.png"
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                throw "Snapshot not found: $source."
            }
            Copy-Item -LiteralPath $source -Destination (Join-Path $screenshotDir $name) -Force
        }

        Write-Host "==> $code`: tour"
        & $tool $snapshotDir $code (Join-Path $gifDir "folderswitch-tour-$code.gif") --frames $framesDir
        if ($LASTEXITCODE -ne 0) {
            throw "The tour of '$code' failed (exit code $LASTEXITCODE)."
        }
    }
}
finally {
    Pop-Location
}
