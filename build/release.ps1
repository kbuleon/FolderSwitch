# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Karl BULEON
# Additional terms under GPLv3 section 7: see the License section of README.md.

<#
.SYNOPSIS
    Prepares and publishes a FolderSwitch version on GitHub Releases (Velopack).

.DESCRIPTION
    Releases the version of the <Version> property of Directory.Build.props, x.y.z only (never a prerelease), as the
    GitHub release "FolderSwitch <version>" of https://github.com/kbuleon/FolderSwitch, on the annotated tag
    v<version>. The application looks for its updates there (UpdateFeed.Url in src/FolderSwitch.App/UpdateFeed.cs).

    Prepare builds locally and uploads nothing: it runs the test projects that open no window (Core, Presentation and
    App, in Release), empties artifacts/releases, downloads the latest published release into it (vpk download
    github, the base of the delta package), writes the version's section of CHANGELOG.md to artifacts/release-notes.md,
    runs build/pack.ps1 with these notes, and records the version and the commit in artifacts/release-prepared.json.
    Try the installer it shows before publishing.

    Publish sends what Prepare built: it creates and pushes the tag v<version> on the prepared commit, uploads
    artifacts/releases as a draft release (vpk upload github), then sets the notes and publishes the release in one
    call (gh release edit). An interrupted Publish can be run again: a tag already on the prepared commit is kept;
    a draft left behind that holds every file vpk uploads (the files of assets.win.json, releases.win.json and
    RELEASES) is published as it is, one that holds none of them is filled (vpk --merge), and one that holds only
    some is refused: delete that draft on GitHub (keep the tag), then run Publish again.

    Both steps first check the repository and refuse, saying what to do, unless: the GitHub CLI (gh) is on the PATH
    and signed in; the branch is main, with no uncommitted change and no untracked file (ignored ones do not count),
    at the same commit as origin/main; the version is x.y.z; CHANGELOG.md has a section for it (titled
    "## <version> - " with an em dash). Prepare also refuses an existing tag v<version> (local or on origin) and an existing release of the version (published or draft), and
    a non-empty artifacts/releases without the marker file .made-by-release-ps1 that Prepare writes into it (it
    empties that folder): set a folder of your own aside (rename it, e.g. to artifacts/releases-before-github) first.
    Publish also refuses a missing preparation or one of another version or commit, a published release of the
    version, several drafts of it, a partly uploaded draft, and a tag v<version> on another commit. The script never
    deletes a tag or a release, and never changes a published release.

    The GitHub token is read from 'gh auth token' once per run and handed to vpk only, in the VPK_TOKEN environment
    variable for the time of the call: never on a command line, never shown, never written to a file. gh uses its
    own sign-in. vpk is a local dotnet tool ('dotnet tool
    restore' is run here). Works in Windows PowerShell 5.1 and in PowerShell 7.

.PARAMETER Step
    Optional. All (the default): Prepare, then Publish, without stopping in between. Prepare: test, download the
    previous release and pack, locally; nothing is uploaded. Publish: tag and upload what Prepare built.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/release.ps1 -Step Prepare

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/release.ps1 -Step Publish

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/release.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Prepare', 'Publish')]
    [string] $Step = 'All'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# PowerShell 7.4 and later: a native command's non-zero exit code is checked here, never made fatal by 'Stop'
# (a missing tag is one). Without effect in Windows PowerShell 5.1.
$PSNativeCommandUseErrorActionPreference = $false

$repoUrl = 'https://github.com/kbuleon/FolderSwitch'
$repoName = 'kbuleon/FolderSwitch'
$releasesDir = 'artifacts/releases'
# Written in $releasesDir by Prepare once emptied: not listed in assets.win.json, so vpk never uploads it.
$releasesMarker = '.made-by-release-ps1'
$notesPath = 'artifacts/release-notes.md'
$preparedPath = 'artifacts/release-prepared.json'
$testProjects = @(
    'tests/FolderSwitch.Core.Tests',
    'tests/FolderSwitch.Presentation.Tests',
    'tests/FolderSwitch.App.Tests'
)

# Written as a code: Windows PowerShell 5.1 reads a script without a byte order mark in the ANSI code page.
$emDash = [char] 0x2014
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# Everything below is relative to the repository root (this script is in <root>/build).
$root = Split-Path -Parent $PSScriptRoot

# Read once per run, by Get-GitHubToken; removed in the finally block at the end.
$token = $null

function Get-RepositoryVersion {
    $propsPath = Join-Path $root 'Directory.Build.props'
    $node = ([xml] (Get-Content -Raw -Encoding UTF8 $propsPath)).SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $node) {
        throw "No <Version> property in $propsPath."
    }
    $node.InnerText.Trim()
}

# The exit code of a native command whose output is not wanted. Windows PowerShell 5.1 turns each line of a
# redirected stderr into an error, which 'Stop' would make fatal: 'Continue' here.
function Invoke-Silently {
    param([string] $FilePath, [string[]] $ArgumentList)
    $ErrorActionPreference = 'Continue'
    & $FilePath @ArgumentList 2>&1 | Out-Null
    $LASTEXITCODE
}

# Runs vpk with the GitHub token in VPK_TOKEN, which vpk reads for --token: never on the command line, which is
# shown as it is run. The variable exists only for the time of the call.
function Invoke-Vpk {
    param([string] $What, [string[]] $ArgumentList, [string] $Failure)
    Write-Host "==> $What`: dotnet vpk $($ArgumentList -join ' ')"
    $env:VPK_TOKEN = Get-GitHubToken
    try {
        dotnet vpk @ArgumentList
        $exitCode = $LASTEXITCODE
    }
    finally {
        Remove-Item Env:VPK_TOKEN -ErrorAction SilentlyContinue
    }
    if ($exitCode -ne 0) {
        throw "vpk $($ArgumentList[0]) $($ArgumentList[1]) failed (exit code $exitCode)$Failure"
    }
}

function Get-GitHubToken {
    if (-not $script:token) {
        $value = gh auth token
        if ($LASTEXITCODE -ne 0 -or -not $value) {
            throw "'gh auth token' gave no token: sign in again with 'gh auth login'."
        }
        $script:token = ([string] ($value | Select-Object -First 1)).Trim()
    }
    $script:token
}

# The section of the version in CHANGELOG.md, from its title '## <version> - ' (em dash) to the line before the next
# title ('## '), without trailing spaces: the title in bold (without its '## '), then the lines below it; $null
# without one.
function Get-ChangelogEntry {
    param([string] $Version)
    $text = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'CHANGELOG.md')
    $start = "## $Version $emDash "
    $entry = $null
    foreach ($line in ($text -split '\r?\n')) {
        if ($null -eq $entry) {
            if ($line.StartsWith($start, [StringComparison]::Ordinal)) {
                $entry = New-Object System.Collections.Generic.List[string]
                $entry.Add('**' + $line.Substring(3).Trim() + '**')
            }
        }
        elseif ($line.StartsWith('## ', [StringComparison]::Ordinal)) {
            break
        }
        else {
            $entry.Add($line.TrimEnd())
        }
    }
    if ($null -eq $entry) {
        return $null
    }
    ($entry -join "`n").TrimEnd()
}

# --- The checks: each one stops the script with a message that says what to do. ---

function Assert-ReleaseVersion {
    param([string] $Version)
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version '$Version' (Directory.Build.props) is not x.y.z: a GitHub release of FolderSwitch is never a prerelease. Set <Version> to x.y.z, or build a prerelease locally with build/pack.ps1 -Version."
    }
}

function Assert-GitHubCli {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "The GitHub CLI (gh) is not on the PATH: install it (winget install --id GitHub.cli), then open a new shell."
    }
    if ((Invoke-Silently gh @('auth', 'status')) -ne 0) {
        throw "The GitHub CLI is not signed in to GitHub: run 'gh auth login' (see 'gh auth status')."
    }
}

function Assert-CleanMain {
    $branch = git rev-parse --abbrev-ref HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "git rev-parse failed (exit code $LASTEXITCODE)."
    }
    if ($branch -ne 'main') {
        throw "The current branch is '$branch': a release is made from main. Switch to it (git switch main)."
    }
    # Untracked files count too (ignored ones do not show): a new file would be built into a release whose tag does
    # not contain it.
    $changes = @(git status --porcelain)
    if ($LASTEXITCODE -ne 0) {
        throw "git status failed (exit code $LASTEXITCODE)."
    }
    if ($changes.Count -gt 0) {
        throw "Uncommitted changes or untracked files ($($changes.Count) path(s)): commit or remove them (or stash the changes) first (git status)."
    }
}

# artifacts/releases is emptied by Prepare: only a folder that release.ps1 made itself (it holds the marker) or an
# empty one is. Anything else (a folder kept by hand, an earlier layout) is left alone.
function Assert-ReleasesDirEmptiable {
    if (-not (Test-Path -LiteralPath $releasesDir)) {
        return
    }
    if (-not (Test-Path -LiteralPath (Join-Path $releasesDir $releasesMarker) -PathType Leaf) -and
        @(Get-ChildItem -LiteralPath $releasesDir -Force).Count -gt 0) {
        throw "$releasesDir is not empty and was not made by release.ps1 (no $releasesMarker in it): Prepare empties it, so set it aside first (rename it, e.g. to artifacts/releases-before-github), then run -Step Prepare again."
    }
}

function Assert-ChangelogEntry {
    param([string] $Version)
    if ($null -eq (Get-ChangelogEntry $Version)) {
        throw "No section for $Version in CHANGELOG.md: add one titled '## $Version $emDash <what it changes>' (its release notes), commit it, then run again."
    }
}

# Returns HEAD, the commit to release.
function Assert-SameAsOrigin {
    git fetch --quiet origin main
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch origin main failed (exit code $LASTEXITCODE): check the network and the access to origin."
    }
    $head = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "git rev-parse HEAD failed (exit code $LASTEXITCODE)."
    }
    $remote = git rev-parse origin/main
    if ($LASTEXITCODE -ne 0) {
        throw "git rev-parse origin/main failed (exit code $LASTEXITCODE)."
    }
    if ($head -ne $remote) {
        $counts = (git rev-list --left-right --count 'HEAD...origin/main') -split '\s+'
        throw "main is $($counts[0]) commit(s) ahead of origin/main and $($counts[1]) behind: push (git push) or pull (git pull) first, so that the release is the commit of origin/main."
    }
    $head
}

# The commit of the tag v<version>, locally and on origin ($null where it does not exist).
function Get-TagState {
    param([string] $Version)
    $tag = "v$Version"

    $local = $null
    $null = git rev-parse -q --verify "refs/tags/$tag"
    if ($LASTEXITCODE -eq 0) {
        $local = git rev-parse "$tag^{commit}"
        if ($LASTEXITCODE -ne 0) {
            throw "git rev-parse $tag^{commit} failed (exit code $LASTEXITCODE)."
        }
    }

    # An annotated tag is listed twice: the tag object, then the commit it points to (^{}).
    $lines = @(git ls-remote --tags origin "refs/tags/$tag" "refs/tags/$tag^{}")
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-remote origin failed (exit code $LASTEXITCODE): check the network and the access to origin."
    }
    $plain = $null
    $peeled = $null
    foreach ($line in $lines) {
        $parts = $line -split '\s+'
        if ($parts.Count -lt 2) {
            continue
        }
        if ($parts[1] -eq "refs/tags/$tag^{}") {
            $peeled = $parts[0]
        }
        elseif ($parts[1] -eq "refs/tags/$tag") {
            $plain = $parts[0]
        }
    }
    $remote = $plain
    if ($peeled) {
        $remote = $peeled
    }

    New-Object PSObject -Property @{ Local = $local; Remote = $remote }
}

# The releases of v<version> on GitHub, drafts included: a list rather than 'gh release view', which shows one
# release only and could not tell that there are several drafts of the same tag.
function Get-VersionReleases {
    param([string] $Version)
    $json = gh release list --repo $repoName --json 'tagName,isDraft' --limit 100
    if ($LASTEXITCODE -ne 0) {
        throw "gh release list failed (exit code $LASTEXITCODE): check the network and 'gh auth status'."
    }
    $all = ($json -join "`n") | ConvertFrom-Json
    $mine = @($all | Where-Object { $_.tagName -eq "v$Version" })
    New-Object PSObject -Property @{
        Published = @($mine | Where-Object { -not $_.isDraft }).Count
        Drafts = @($mine | Where-Object { $_.isDraft }).Count
    }
}

# The files vpk upload github sends for the version, from artifacts/releases: those of assets.win.json (installer,
# portable zip, packages), each with its size, then the feed releases.win.json and the legacy RELEASES, which vpk
# writes itself (their size is not compared).
function Get-UploadFiles {
    $assetsPath = "$releasesDir/assets.win.json"
    if (-not (Test-Path $assetsPath)) {
        throw "$assetsPath is missing: run -Step Prepare again."
    }
    $entries = (Get-Content -Raw -Encoding UTF8 $assetsPath) | ConvertFrom-Json
    $files = @()
    foreach ($entry in @($entries | Where-Object { $_ })) {
        $path = Join-Path $releasesDir $entry.RelativeFileName
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "$path, listed in $assetsPath, is missing: run -Step Prepare again."
        }
        $files += New-Object PSObject -Property @{ Name = (Split-Path -Leaf $path); Size = (Get-Item -LiteralPath $path).Length }
    }
    if ($files.Count -eq 0) {
        throw "$assetsPath lists no file: run -Step Prepare again."
    }
    foreach ($name in @('releases.win.json', 'RELEASES')) {
        $files += New-Object PSObject -Property @{ Name = $name; Size = $null }
    }
    $files
}

# How many of the files the draft holds, complete: same name, fully uploaded, same size where it is compared.
# Found counts the files present in any state. Assets is the 'assets' array of 'gh release view --json assets'.
function Compare-DraftAssets {
    param([object[]] $Assets, [object[]] $Files)
    $present = @($Assets | Where-Object { $_ })
    $complete = 0
    $found = 0
    foreach ($file in $Files) {
        $asset = @($present | Where-Object { $_.name -eq $file.Name }) | Select-Object -First 1
        if (-not $asset) {
            continue
        }
        $found++
        $stateProperty = $asset.PSObject.Properties['state']
        $uploaded = (-not $stateProperty) -or ($stateProperty.Value -eq 'uploaded')
        $sizeProperty = $asset.PSObject.Properties['size']
        $sameSize = ($null -eq $file.Size) -or ($sizeProperty -and $sizeProperty.Value -eq $file.Size)
        if ($uploaded -and $sameSize) {
            $complete++
        }
    }
    New-Object PSObject -Property @{ Complete = $complete; Found = $found; Total = $Files.Count }
}

# The assets of the draft of v<version> (gh finds a draft by its tag).
function Get-DraftAssets {
    param([string] $Version)
    $json = gh release view "v$Version" --repo $repoName --json 'isDraft,assets'
    if ($LASTEXITCODE -ne 0) {
        throw "gh release view v$Version failed (exit code $LASTEXITCODE): check the network and 'gh auth status'."
    }
    $draft = ($json -join "`n") | ConvertFrom-Json
    if (-not $draft.isDraft) {
        throw "The release v$Version on GitHub is no longer a draft: check it on GitHub."
    }
    @($draft.assets | Where-Object { $_ })
}

# The checks of both steps; returns HEAD, its only output.
function Assert-ReadyToRelease {
    param([string] $Version)
    $null = Assert-ReleaseVersion $Version
    $null = Assert-GitHubCli
    $null = Assert-CleanMain
    $null = Assert-ChangelogEntry $Version
    $head = [string] (@(Assert-SameAsOrigin) | Select-Object -Last 1)
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw "Unexpected commit '$head' from git rev-parse HEAD."
    }
    return $head
}

# --- The steps. ---

function Invoke-Prepare {
    param([string] $Version)
    Write-Host "==> Checking the repository for FolderSwitch $Version"
    $commit = Assert-ReadyToRelease $Version

    $tags = Get-TagState $Version
    if ($tags.Local -or $tags.Remote) {
        $where = @()
        if ($tags.Local) { $where += 'locally' }
        if ($tags.Remote) { $where += 'on origin' }
        throw "The tag v$Version already exists $($where -join ' and '): a release of $Version was started. Run -Step Publish to complete it, or bump <Version> in Directory.Build.props for a new one."
    }
    $releases = Get-VersionReleases $Version
    if ($releases.Published -gt 0) {
        throw "FolderSwitch $Version is already published on GitHub: bump <Version> in Directory.Build.props for a new release."
    }
    if ($releases.Drafts -gt 0) {
        throw "A draft release of v$Version exists on GitHub (an interrupted Publish): run -Step Publish to complete it, or delete the draft on GitHub to prepare again."
    }

    # Before the tests: refuse early rather than after them.
    Assert-ReleasesDirEmptiable

    foreach ($project in $testProjects) {
        Write-Host "==> Testing $project"
        dotnet test $project -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test $project failed (exit code $LASTEXITCODE): fix it before releasing."
        }
    }

    Write-Host "==> Emptying $releasesDir"
    if (Test-Path $preparedPath) {
        Remove-Item -Force $preparedPath
    }
    Assert-ReleasesDirEmptiable
    if (Test-Path $releasesDir) {
        Get-ChildItem -LiteralPath $releasesDir -Force | Remove-Item -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $releasesDir | Out-Null
    }
    [System.IO.File]::WriteAllText((Join-Path (Join-Path $root $releasesDir) $releasesMarker), "Made by build/release.ps1: Prepare may empty this folder.`n", $utf8NoBom)

    Write-Host '==> Restoring the local tools (vpk)'
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed (exit code $LASTEXITCODE)."
    }

    # The latest published release: vpk pack builds the delta package from it. None before the first release.
    Invoke-Vpk 'Downloading the latest published release' @('download', 'github', '--repoUrl', $repoUrl, '-o', $releasesDir) '.'

    Write-Host "==> Writing the release notes to $notesPath"
    [System.IO.File]::WriteAllText((Join-Path $root $notesPath), (Get-ChangelogEntry $Version) + "`n", $utf8NoBom)

    & (Join-Path $PSScriptRoot 'pack.ps1') -ReleaseNotes $notesPath

    $prepared = New-Object PSObject -Property ([ordered] @{ version = $Version; commit = $commit })
    [System.IO.File]::WriteAllText((Join-Path $root $preparedPath), ($prepared | ConvertTo-Json), $utf8NoBom)

    $setup = Join-Path $root 'artifacts\releases\FolderSwitch-win-Setup.exe'
    Write-Host "==> Prepared FolderSwitch $Version (commit $commit): $setup"
}

function Invoke-Publish {
    param([string] $Version)
    $tag = "v$Version"

    Write-Host "==> Checking the preparation of FolderSwitch $Version"
    if (-not (Test-Path $preparedPath)) {
        throw "No preparation ($preparedPath): run -Step Prepare first."
    }
    $prepared = Get-Content -Raw -Encoding UTF8 $preparedPath | ConvertFrom-Json
    if ($prepared.version -ne $Version) {
        throw "$preparedPath is for version $($prepared.version), and Directory.Build.props says $Version`: run -Step Prepare again."
    }
    $head = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "git rev-parse HEAD failed (exit code $LASTEXITCODE)."
    }
    $commit = $prepared.commit
    if ($commit -ne $head) {
        throw "$preparedPath was made at commit $commit, and HEAD is $head`: run -Step Prepare again."
    }
    foreach ($file in @($notesPath, "$releasesDir/FolderSwitch-$Version-full.nupkg")) {
        if (-not (Test-Path $file)) {
            throw "$file is missing: run -Step Prepare again."
        }
    }
    $files = Get-UploadFiles

    Write-Host "==> Checking the repository for FolderSwitch $Version"
    $null = Assert-ReadyToRelease $Version

    $releases = Get-VersionReleases $Version
    if ($releases.Published -gt 0) {
        throw "FolderSwitch $Version is already published on GitHub: nothing to do (a published release is never changed)."
    }
    if ($releases.Drafts -gt 1) {
        throw "$($releases.Drafts) draft releases of $tag exist on GitHub: delete all of them but one on GitHub, then run -Step Publish again."
    }

    # A draft left by an interrupted Publish: vpk can neither merge into one that holds its feed nor upload a file
    # twice, so a draft is filled only when empty of these files, and published as it is only when it holds them all.
    # Upload: New (no draft), Merge (an empty draft) or None (a complete draft).
    $upload = 'New'
    if ($releases.Drafts -eq 1) {
        $held = Compare-DraftAssets (Get-DraftAssets $Version) $files
        if ($held.Complete -eq $held.Total) {
            $upload = 'None'
        }
        elseif ($held.Found -eq 0) {
            $upload = 'Merge'
        }
        else {
            throw "The draft of $tag on GitHub holds $($held.Complete) of $($held.Total) files: delete that draft on GitHub (keep the tag $tag), then run -Step Publish again."
        }
    }

    $tags = Get-TagState $Version
    if ($tags.Local -and $tags.Local -ne $commit) {
        throw "The local tag $tag is on commit $($tags.Local), not on the prepared commit $commit`: check it (git show $tag); delete it by hand if it is wrong (git tag -d $tag), or bump <Version>."
    }
    if ($tags.Remote -and $tags.Remote -ne $commit) {
        throw "The tag $tag on origin is on commit $($tags.Remote), not on the prepared commit $commit`: check it on GitHub; delete it there by hand if it is wrong, or bump <Version>."
    }

    if (-not $tags.Local) {
        Write-Host "==> Tagging $tag on $commit"
        git tag -a $tag -m "FolderSwitch $Version" $commit
        if ($LASTEXITCODE -ne 0) {
            throw "git tag failed (exit code $LASTEXITCODE)."
        }
    }
    if (-not $tags.Remote) {
        Write-Host "==> Pushing $tag to origin"
        git push origin $tag
        if ($LASTEXITCODE -ne 0) {
            throw "git push origin $tag failed (exit code $LASTEXITCODE): run -Step Publish again."
        }
    }

    if ($upload -eq 'None') {
        Write-Host "==> The draft release $tag already holds the $($files.Count) files: not uploaded again"
    }
    else {
        Write-Host '==> Restoring the local tools (vpk)'
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet tool restore failed (exit code $LASTEXITCODE)."
        }

        # A draft: the release becomes visible only below, with its notes.
        $uploadArgs = @(
            'upload', 'github',
            '--repoUrl', $repoUrl,
            '-o', $releasesDir,
            '--releaseName', "FolderSwitch $Version",
            '--tag', $tag,
            '--targetCommitish', $commit
        )
        $what = "Uploading $releasesDir as a draft"
        if ($upload -eq 'Merge') {
            $uploadArgs += '--merge'
            $what = "Uploading $releasesDir into the empty draft $tag"
        }
        Invoke-Vpk $what $uploadArgs ": run -Step Publish again; if it refuses a partly uploaded draft, delete that draft on GitHub (keep the tag $tag) first."
    }

    Write-Host "==> Publishing $tag with its notes"
    gh release edit $tag --repo $repoName --notes-file $notesPath --draft=false --latest
    if ($LASTEXITCODE -ne 0) {
        throw "gh release edit failed (exit code $LASTEXITCODE): run -Step Publish again; the draft holds every file and is published without uploading them again."
    }

    $json = gh release view $tag --repo $repoName --json 'isDraft,isPrerelease,name'
    if ($LASTEXITCODE -ne 0) {
        throw "gh release view failed (exit code $LASTEXITCODE): check the release on GitHub."
    }
    $view = ($json -join "`n") | ConvertFrom-Json
    if ($view.isDraft -or $view.isPrerelease -or $view.name -ne "FolderSwitch $Version") {
        throw "The release $tag is not as expected (draft: $($view.isDraft), prerelease: $($view.isPrerelease), name: '$($view.name)'): check it on GitHub."
    }

    Write-Host "==> Published: $repoUrl/releases/tag/$tag"
}

Push-Location $root
try {
    $version = Get-RepositoryVersion
    if ($Step -eq 'All' -or $Step -eq 'Prepare') {
        Invoke-Prepare $version
    }
    if ($Step -eq 'All' -or $Step -eq 'Publish') {
        Invoke-Publish $version
    }
}
finally {
    Remove-Variable token -ErrorAction SilentlyContinue
    Remove-Item Env:VPK_TOKEN -ErrorAction SilentlyContinue
    Pop-Location
}
