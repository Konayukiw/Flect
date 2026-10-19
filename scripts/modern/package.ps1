$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$repo = $ModernMenuRepo
Build-ModernMenu
$layout = Join-Path $repo 'build\modern\layout'
$assets = Join-Path $layout 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'setup\modern\AppxManifest.xml') -Destination $layout -Force
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
& $makeappx pack /d $layout /p $package /nv /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX creation failed.' }
$subject = 'CN=Flect Local Menu'
$cert = Get-ChildItem -Path 'Cert:\CurrentUser\My' -CodeSigningCert |
    Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (!$cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $subject `
        -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 `
        -HashAlgorithm SHA256 -KeyExportPolicy NonExportable `
        -FriendlyName 'Flect local context menu' -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(2) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
$publicCert = Join-Path $repo 'build\modern\FlectModernMenu.cer'
Export-Certificate -Cert $cert -FilePath $publicCert -Force | Out-Null
$signtool = Get-ModernMenuSdkTool 'signtool.exe'
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $package
if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed.' }
$signature = Get-AuthenticodeSignature -LiteralPath $package
if (!$signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
    throw 'The MSIX signer does not match the local Flect certificate.'
}
Write-Output ('Signed with local certificate: ' + $cert.Thumbprint)
Write-Output ('Packaged: ' + $package)
