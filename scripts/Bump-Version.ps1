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

.EXAMPLE
    ./scripts/Bump-Version.ps1 patch
    ./scripts/Bump-Version.ps1 prerelease
    ./scripts/Bump-Version.ps1 prerelease -Label rc
    ./scripts/Bump-Version.ps1 release
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('major','minor','patch','prerelease','release')]
    [string] $Part,

    [string] $Label = 'beta'
)

$file    = Join-Path $PSScriptRoot '..\version.txt'
$current = (Get-Content $file -Raw).Trim()

if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-([a-zA-Z]+)\.(\d+))?$') {
    Write-Error "Cannot parse version '$current'. Expected format: 1.2.3 or 1.2.3-beta.1"
    exit 1
}

$major    = [int]$Matches[1]
$minor    = [int]$Matches[2]
$patch    = [int]$Matches[3]
$preLabel = $Matches[4]   # '' when stable
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

Set-Content -NoNewline $file $new
Write-Host "$current → $new"
