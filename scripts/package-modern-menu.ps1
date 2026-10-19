$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'modern-menu-common.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$layout = Join-Path $repo 'build\modern\layout'
$assets = Join-Path $layout 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'modern-package\AppxManifest.xml') -Destination $layout -Force
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Icon]::new((Join-Path $repo 'src\App\Assets\flect.ico'), 256, 256)
$source = $icon.ToBitmap()
foreach ($entry in @(@('StoreLogo', 50), @('Logo44', 44), @('Logo150', 150))) {
    $bitmap = [Drawing.Bitmap]::new([int]$entry[1], [int]$entry[1])
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, 0, 0, [int]$entry[1], [int]$entry[1])
    $bitmap.Save((Join-Path $assets ($entry[0] + '.png')), [Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}
$source.Dispose()
$icon.Dispose()
$makeappx = Get-ModernMenuSdkTool 'makeappx.exe'
$package = Join-Path $repo 'build\modern\FlectModernMenu.msix'
# /nv is required for a sparse package: executable files live at ExternalLocation.
& $makeappx pack /d $layout /p $package /nv /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX creation failed.' }
& (Join-Path $PSScriptRoot 'sign-modern-menu.ps1')
Write-Output ('Packaged: ' + $package)
