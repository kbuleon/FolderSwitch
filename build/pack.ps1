# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Karl BULEON
# Additional terms under GPLv3 section 7: see the License section of README.md.

<#
.SYNOPSIS
    Builds the FolderSwitch installer and update packages (Velopack).

.DESCRIPTION
    Publishes the application (self-contained, ReadyToRun, win-x64) to artifacts/publish, then packs it with
    Velopack's vpk tool into artifacts/releases: the installer (FolderSwitch-win-Setup.exe), the full package
    (FolderSwitch-<version>-full.nupkg), a delta package from the previous version found in artifacts/releases,
    and the feed file (releases.win.json). The content of artifacts/releases is what build/release.ps1 uploads to the
    GitHub release of the version (see UpdateFeed.Url in src/FolderSwitch.App/UpdateFeed.cs).

    The delta package is built from whatever previous release artifacts/releases holds. build/release.ps1 rebuilds
    that folder at each preparation, from the last release published on GitHub; run alone, this script keeps what is
    there. artifacts/publish is rebuilt from scratch each time.

    vpk is a local dotnet tool (.config/dotnet-tools.json, same version as the Velopack package): nothing to install
    by hand, 'dotnet tool restore' is run here. Works in Windows PowerShell 5.1 and in PowerShell 7.

.PARAMETER Version
    Optional. The version to release, x.y.z or x.y.z-prerelease (for instance 0.1.0 or 0.2.0-beta.1). It is also the
    version of the application (shown in the settings). Defaults to the <Version> property of Directory.Build.props;
    pass it only to release another one, such as a prerelease.

.PARAMETER SignParams
    Optional. Parameters passed to signtool.exe (vpk runs its embedded copy) to sign the application's files, the
    updater and the installer, for instance '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256' with the code
    signing certificate in the user's store. Not signed when omitted.

.PARAMETER ReleaseNotes
    Optional. A Markdown file with the notes of this version, which must exist; vpk puts them in the package
    (--releaseNotes). build/release.ps1 passes the version's section of CHANGELOG.md. No notes when omitted.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/pack.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/pack.ps1 -Version 0.2.0-beta.1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/pack.ps1 -ReleaseNotes artifacts/release-notes.md
#>
[CmdletBinding()]
param(
    [string] $Version,

    [string] $SignParams,

    [string] $ReleaseNotes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# A full path, resolved from where the script is called: vpk runs from the repository root.
if ($ReleaseNotes) {
    if (-not (Test-Path -LiteralPath $ReleaseNotes -PathType Leaf)) {
        throw "Release notes file not found: $ReleaseNotes."
    }
    $ReleaseNotes = (Resolve-Path -LiteralPath $ReleaseNotes).ProviderPath
}

# The version of the repository, unless another one is asked for.
if (-not $Version) {
    $propsPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'Directory.Build.props'
    $node = ([xml] (Get-Content -Raw -Encoding UTF8 $propsPath)).SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $node) {
        throw "No <Version> property in $propsPath."
    }
    $Version = $node.InnerText.Trim()
}

# x.y.z or x.y.z-prerelease (semantic versioning, no build metadata): what Velopack and NuGet accept.
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$') {
    throw "Invalid version '$Version': expected x.y.z or x.y.z-prerelease (for instance 0.1.0 or 0.2.0-beta.1)."
}

# Everything below is relative to the repository root (this script is in <root>/build).
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $publishDir = 'artifacts/publish'
    $releasesDir = 'artifacts/releases'

    # Never pack what an earlier build left behind.
    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }

    Write-Host "==> Publishing FolderSwitch $Version"
    dotnet publish src/FolderSwitch.App -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true "-p:Version=$Version" -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed (exit code $LASTEXITCODE)."
    }

    Write-Host '==> Restoring the local tools (vpk)'
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed (exit code $LASTEXITCODE)."
    }

    # -r: the architecture of the package; without it vpk assumes x86 (FolderSwitch is x64 only).
    $vpkArgs = @(
        'vpk', 'pack',
        '-u', 'FolderSwitch',
        '-v', $Version,
        '-p', $publishDir,
        '-e', 'FolderSwitch.exe',
        '-i', 'assets/icon/FolderSwitch.ico',
        '-o', $releasesDir,
        '-r', 'win-x64'
    )

    if ($ReleaseNotes) {
        $vpkArgs += @('--releaseNotes', $ReleaseNotes)
    }

    if ($SignParams) {
        # Windows PowerShell 5.1 (and PowerShell 7 before 7.3) hands a double quote of an argument to the program
        # unescaped: escape them, as a path with spaces in the signtool parameters would need.
        $signArgument = $SignParams
        $ps = $PSVersionTable.PSVersion
        if ($ps.Major -lt 7 -or ($ps.Major -eq 7 -and $ps.Minor -lt 3)) {
            $signArgument = $SignParams.Replace('"', '\"')
        }

        $vpkArgs += @('--signParams', $signArgument)
        Write-Host '==> Packing with Velopack (signed)'
    }
    else {
        Write-Host '==> Packing with Velopack (not signed)'
    }

    dotnet @vpkArgs
    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack failed (exit code $LASTEXITCODE)."
    }

    Write-Host "==> Done: $releasesDir"
    Get-ChildItem $releasesDir | ForEach-Object { '{0,12:N0}  {1}' -f $_.Length, $_.Name }
}
finally {
    Pop-Location
}
