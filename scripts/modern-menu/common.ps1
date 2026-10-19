# Helpers shared by the optional Windows 11 menu build and deployment scripts.
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
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $ModernMenuRepo 'setup\modern-menu\AppxManifest.xml')
    return [string]$manifest.Package.Identity.Name
}

function Get-ModernMenuAppDirectory {
    $brand = Get-ModernMenuBrand
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $brand.BrandAppId + '_is1'
    $installed = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
    if ($installed.InstallLocation) { return [string]$installed.InstallLocation }
    return Join-Path $env:LOCALAPPDATA $brand.BrandName
}
