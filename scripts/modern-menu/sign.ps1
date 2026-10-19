$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$repo = $ModernMenuRepo
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
$package = Join-Path $repo 'build\modern\FlectModernMenu.msix'
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $package
if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed.' }
$signature = Get-AuthenticodeSignature -LiteralPath $package
if (!$signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
    throw 'The MSIX signer does not match the local Flect certificate.'
}
Write-Output ('Signed with local certificate: ' + $cert.Thumbprint)
