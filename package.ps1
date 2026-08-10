<#
.SYNOPSIS
    Packages dist\ into a single-file installer.

.DESCRIPTION
    Reads the product name, install identity and version out of
    Directory.Build.props and hands them to Inno Setup, so the installer
    script contains no copy of the name either.

    Run .\build.ps1 first. The installer is written to build\setup\.
#>

[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

[xml]$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
$group = $props.Project.PropertyGroup | Where-Object { $_.BrandName } | Select-Object -First 1
$brand = $group.BrandName
$appId = $group.BrandAppId
if (-not $brand) { throw 'BrandName is missing from Directory.Build.props' }
if (-not $appId) { throw 'BrandAppId is missing from Directory.Build.props' }
if (-not $Version) { $Version = $group.BrandVersion }
if (-not $Version) { $Version = '1.0' }

$appId = $appId.Trim('{', '}')

$dist = Join-Path $repoRoot 'dist'
$outputDir = Join-Path $repoRoot 'build\setup'

if (-not (Test-Path (Join-Path $dist "${brand}Shell.dll"))) {
    throw "dist\ is empty or stale. Run .\build.ps1 first."
}

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup was not found. Install it with: winget install JRSoftware.InnoSetup'
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
Write-Host "Packaging $brand $Version" -ForegroundColor Cyan

$isccArgs = @(
    "/DBrandName=$brand"
    "/DAppId=$appId"
    "/DAppVersion=$Version"
    "/DDistDir=$dist"
    "/DOutputDir=$outputDir"
)

$icon = Join-Path $repoRoot 'src\App\Assets\flect.ico'
if (Test-Path $icon) { $isccArgs += "/DIconFile=$icon" }

$isccArgs += (Join-Path $repoRoot 'setup\setup.iss')

& $iscc @isccArgs

if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

$setup = Get-ChildItem $outputDir -Filter "$brand-$Version-setup.exe" | Select-Object -First 1
Write-Host ("Built {0} ({1:N0} MB)" -f $setup.FullName, ($setup.Length / 1MB)) -ForegroundColor Green
