<#
.SYNOPSIS
    Builds the shell extension and the worker, and stages both into dist\.

.DESCRIPTION
    The product name is never written here. It is read out of
    Directory.Build.props, which is the single place it is defined.

.PARAMETER SelfContained
    Carries a private copy of the .NET runtime instead of relying on an
    installed .NET 10 Desktop Runtime. Removes the prerequisite and adds
    roughly 140 MB to dist\.
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

[xml]$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
$brand = ($props.Project.PropertyGroup | Where-Object { $_.BrandName } | Select-Object -First 1).BrandName
if (-not $brand) { throw 'BrandName is missing from Directory.Build.props' }

$shellDll = "${brand}Shell.dll"
$dist = Join-Path $repoRoot 'dist'

Write-Host "Building $brand" -ForegroundColor Cyan

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'vswhere.exe not found. Visual Studio Build Tools are required.' }

$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
                     -property installationPath | Select-Object -First 1
if (-not $vsPath) { throw 'No Visual Studio installation with the C++ toolset was found.' }

$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe'
Write-Host 'shell extension...'
& $msbuild (Join-Path $repoRoot 'src\Shell\ShellExt.vcxproj') `
    /p:Configuration=$Configuration /p:Platform=x64 /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'The shell extension failed to build.' }

Write-Host 'worker...'
Remove-Item -Recurse -Force $dist -ErrorAction SilentlyContinue

$publishArgs = @(
    'publish', (Join-Path $repoRoot 'src\App\App.csproj'),
    '-c', $Configuration,
    '-r', 'win-x64',
    '-o', $dist,
    '--nologo', '-v:minimal'
)
if ($SelfContained) { $publishArgs += @('--self-contained', 'true') }
else { $publishArgs += @('--self-contained', 'false') }

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'The worker failed to build.' }

Copy-Item (Join-Path $repoRoot "build\shell\$Configuration\$shellDll") -Destination $dist -Force

$thirdParty = Join-Path $repoRoot 'dependencies'
$toolsOut = Join-Path $dist 'tools'
New-Item -ItemType Directory -Force -Path $toolsOut | Out-Null

if (Test-Path $thirdParty) {
    Get-ChildItem $thirdParty -File | Copy-Item -Destination $toolsOut -Force
}

$pythonTool = Join-Path $repoRoot 'tools\remove_bg.py'
if (Test-Path $pythonTool) {
    Copy-Item $pythonTool -Destination $toolsOut -Force
    Write-Host 'remove_bg.py staged'
}

$missing = @('ffmpeg.exe', 'ffprobe.exe', 'cfr.jar', '7za.exe', '7z.exe') |
    Where-Object { -not (Test-Path (Join-Path $toolsOut $_)) }
if ($missing) {
    Write-Host ("Note: {0} not bundled - run fetch\get.ps1. " -f ($missing -join ', ')) `
        -ForegroundColor Yellow
    Write-Host 'Until then those commands fall back to whatever is on PATH.' -ForegroundColor Yellow
}

$size = (Get-ChildItem $dist -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Staged {0} ({1:N0} MB)" -f $dist, ($size / 1MB)) -ForegroundColor Green
Write-Host 'Next: .\install.ps1'
