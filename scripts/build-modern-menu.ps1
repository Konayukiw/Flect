param([switch]$WithTests)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'modern-menu-common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$generated = Join-Path $repo 'build\modern\generated'
New-Item -ItemType Directory -Path $generated -Force | Out-Null
$brand = Get-ModernMenuBrand
@"
#pragma once
#define BRAND_NAME L"$($brand.BrandName)"
#define BRAND_CLSID_STR L"$($brand.BrandClsid)"
#define BRAND_APP_EXE L"$($brand.BrandName).exe"
#define BRAND_UNINSTALL_KEY L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\$($brand.BrandAppId)_is1"
"@ | Set-Content -LiteralPath (Join-Path $generated 'Branding.g.h') -Encoding utf8
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio C++ build tools were not found.' }
$projects = @('ModernShell', 'MenuHost')
if ($WithTests) { $projects += 'SmokeTest' }
foreach ($project in $projects) {
    & $msbuild (Join-Path $repo "src\ModernShell\$project.vcxproj") /p:Configuration=Release /p:Platform=x64 /m /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw "$project build failed." }
}
Write-Output ('Built: ' + (Join-Path $repo 'build\modern\bin'))
