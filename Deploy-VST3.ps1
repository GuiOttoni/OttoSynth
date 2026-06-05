#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Deploy OttoSynth VST3 plugin to the system VST3 folder.
    Must be run as Administrator.

.DESCRIPTION
    Copies the Release build artifacts to C:\Program Files\Common Files\VST3\OttoSynth\.
    The runtimeconfig.json is the most critical file — it controls which .NET Desktop
    Runtime version is accepted. The correct config uses rollForward=LatestMajor with
    version=8.0.0, which means any .NET 8, 9 or 10 Desktop Runtime is accepted.
    The wrong config (LatestMinor + version=10.0.0) requires exactly .NET 10, causing
    a KERNELBASE.dll crash on machines that only have .NET 8 or .NET 9 installed.
#>

param(
    [string]$BuildConfig = "Release",
    [string]$BuildTfm    = "net10.0-windows",
    [string]$Destination = "C:\Program Files\Common Files\VST3\OttoSynth"
)

$ErrorActionPreference = "Stop"
$src = "$PSScriptRoot\src\OttoSynth.Plugin\bin\$BuildConfig\$BuildTfm"

if (-not (Test-Path $src)) {
    Write-Error "Build output not found at '$src'. Run: dotnet build src/OttoSynth.Plugin -c $BuildConfig"
}

Write-Host "Source : $src"
Write-Host "Target : $Destination"
Write-Host ""

New-Item -ItemType Directory -Path $Destination -Force | Out-Null

$files = @(
    # Managed plugin assemblies (updated code)
    "OttoSynth.dll",
    "OttoSynth.Core.dll",
    "OttoSynth.UI.dll",
    "OttoSynth.deps.json",

    # THE KEY FIX: runtimeconfig with rollForward=LatestMajor + version=8.0.0
    # This allows the plugin to load under .NET 8, 9 or 10 Desktop Runtime.
    # The previously installed version used rollForward=LatestMinor + version=10.0.0
    # which required exactly .NET 10 and caused KERNELBASE.dll crashes on machines
    # that only have .NET 8 or .NET 9.
    "OttoSynthBridge.runtimeconfig.json",
    "OttoSynth.PluginBridge.runtimeconfig.json"
)

foreach ($file in $files) {
    $srcFile = Join-Path $src $file
    if (Test-Path $srcFile) {
        Copy-Item $srcFile $Destination -Force
        Write-Host "  [OK] $file"
    } else {
        Write-Host "  [--] $file (not found in build output, skipping)"
    }
}

Write-Host ""
Write-Host "Deploy complete. Restart your DAW (Cubase / Pro Tools / FL Studio) and rescan plugins."
Write-Host ""
Write-Host "Installed runtimeconfig:"
Get-Content (Join-Path $Destination "OttoSynthBridge.runtimeconfig.json") | Write-Host
