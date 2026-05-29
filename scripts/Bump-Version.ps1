<#
.SYNOPSIS
    Bumps version.txt following semver (major.minor.patch[-label.n]).

.PARAMETER Part
    major   — 1.2.3        → 2.0.0
    minor   — 1.2.3        → 1.3.0
    patch   — 1.2.3        → 1.2.4
    prerelease — 1.2.3     → 1.2.3-beta.1
               — 1.2.3-beta.1 → 1.2.3-beta.2
    release  — 1.2.3-beta.2 → 1.2.3

.PARAMETER Label
    Prerelease label used when promoting a stable version to prerelease.
    Defaults to 'beta'.

.PARAMETER FilePath
    Absolute path to version.txt. Defaults to ../version.txt relative to
    this script. Pass an absolute path from CI to avoid PSScriptRoot
    resolution issues on hosted runners.

.EXAMPLE
    ./scripts/Bump-Version.ps1 patch
    ./scripts/Bump-Version.ps1 prerelease
    ./scripts/Bump-Version.ps1 prerelease -Label rc
    ./scripts/Bump-Version.ps1 release
    ./scripts/Bump-Version.ps1 patch -FilePath D:\a\OttoSynth\OttoSynth\version.txt
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('major','minor','patch','prerelease','release')]
    [string] $Part,

    [string] $Label = 'beta',

    [string] $FilePath = ''
)

if ($FilePath -eq '') {
    $FilePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\version.txt'))
}

Write-Host "version.txt path: $FilePath"

if (-not (Test-Path $FilePath)) {
    Write-Error "version.txt not found at '$FilePath'"
    exit 1
}

$current = (Get-Content $FilePath -Raw).Trim()

if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-([a-zA-Z]+)\.(\d+))?$') {
    Write-Error "Cannot parse version '$current'. Expected format: 1.2.3 or 1.2.3-beta.1"
    exit 1
}

$major    = [int]$Matches[1]
$minor    = [int]$Matches[2]
$patch    = [int]$Matches[3]
$preLabel = $Matches[4]
$preNum   = if ($Matches[5]) { [int]$Matches[5] } else { 0 }

$new = switch ($Part) {
    'major'      { $major++; $minor = 0; $patch = 0; "$major.$minor.$patch" }
    'minor'      { $minor++;             $patch = 0; "$major.$minor.$patch" }
    'patch'      { $patch++;                         "$major.$minor.$patch" }
    'prerelease' {
        if ($preLabel) {
            "$major.$minor.$patch-$preLabel.$($preNum + 1)"
        } else {
            "$major.$minor.$patch-$Label.1"
        }
    }
    'release'    { "$major.$minor.$patch" }
}

[System.IO.File]::WriteAllText($FilePath, $new)
$verify = (Get-Content $FilePath -Raw).Trim()
if ($verify -ne $new) {
    Write-Error "Write verification failed: expected '$new' but got '$verify' at '$FilePath'"
    exit 1
}
Write-Host "$current → $new"
