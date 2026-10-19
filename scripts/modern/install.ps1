param([switch]$RegisterOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$repo = $ModernMenuRepo
$destination = Join-Path $env:LOCALAPPDATA 'FlectModernMenu'
$package = Join-Path $repo 'build\modern\FlectModernMenu.msix'
$signature = Get-AuthenticodeSignature -LiteralPath $package
if ($signature.Status -ne 'Valid') {
    throw 'The Flect package signature is not trusted yet. Run register-elevated.ps1 to install its local signing certificate and register the menu.'
}
if (!$RegisterOnly) {
    $brand = Get-ModernMenuBrand
    if (!(Test-Path -LiteralPath (Join-Path (Get-ModernMenuAppDirectory) ($brand.BrandName + '.exe')))) { throw 'Install Flect first.' }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($name in @('FlectModernShell.dll', 'FlectMenu.exe')) {
        $source = Join-Path $repo ('build\modern\bin\' + $name)
        $target = Join-Path $destination $name
        if (!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
            Copy-Item -LiteralPath $source -Destination $target -Force
        }
    }
    Copy-Item -LiteralPath (Join-Path $repo 'build\modern\layout\Assets') -Destination $destination -Recurse -Force
}
Add-AppxPackage -Path $package -ExternalLocation $destination -ErrorAction Stop
Get-AppxPackage -Name (Get-ModernMenuPackageName) | Select-Object Name, PackageFullName, Status
