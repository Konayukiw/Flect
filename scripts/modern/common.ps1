$ModernMenuRepo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

function Get-ModernMenuSdkTool([string]$Name) {
    $kits = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -ErrorAction SilentlyContinue
    $roots = @($env:WindowsSdkDir, $kits.KitsRoot10, (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10')) |
        Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'bin')) } | Select-Object -Unique
    foreach ($root in $roots) {
        $versions = Get-ChildItem -LiteralPath (Join-Path $root 'bin') -Directory |
            Where-Object { $_.Name -match '^10\.\d+\.\d+\.\d+$' } |
            Sort-Object { [version]$_.Name } -Descending
        foreach ($version in $versions) {
            $candidate = Join-Path $version.FullName ('x64\' + $Name)
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    throw "Windows SDK tool $Name was not found. Install a Windows 10/11 SDK."
}

function Get-ModernMenuBrand {
    [xml]$props = Get-Content -LiteralPath (Join-Path $ModernMenuRepo 'Directory.Build.props')
    return $props.Project.PropertyGroup | Where-Object { $_.BrandName } | Select-Object -First 1
}

function Get-ModernMenuPackageName {
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $ModernMenuRepo 'setup\modern\AppxManifest.xml')
    return [string]$manifest.Package.Identity.Name
}

function Get-ModernMenuAppDirectory {
    $brand = Get-ModernMenuBrand
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $brand.BrandAppId + '_is1'
    $installed = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
    if ($installed.InstallLocation) { return [string]$installed.InstallLocation }
    return Join-Path $env:LOCALAPPDATA $brand.BrandName
}

function Build-ModernMenu([switch]$WithTests) {
    $generated = Join-Path $ModernMenuRepo 'build\modern\generated'
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
        & $msbuild (Join-Path $ModernMenuRepo "src\Shell\Modern\$project.vcxproj") /p:Configuration=Release /p:Platform=x64 /m /nologo /verbosity:minimal
        if ($LASTEXITCODE -ne 0) { throw "$project build failed." }
    }
    Write-Output ('Built: ' + (Join-Path $ModernMenuRepo 'build\modern\bin'))
}
