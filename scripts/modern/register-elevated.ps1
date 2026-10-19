param([switch]$Elevated)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$repo = $ModernMenuRepo
$log = Join-Path $repo 'build\modern\registration.log'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($Elevated) { throw 'Administrator privileges were not granted.' }
    Write-Output 'Windows will request administrator permission to trust the local Flect signing certificate and register the menu.'
    try {
        $child = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
            -Verb RunAs -WindowStyle Hidden -PassThru `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-Elevated')
        $child.WaitForExit()
        if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 45 }
        exit $child.ExitCode
    } catch {
        Write-Output ('Registration did not complete: ' + $_.Exception.Message)
        exit 1
    }
}
Start-Transcript -LiteralPath $log -Force | Out-Null
try {
    $publicCert = Join-Path $repo 'build\modern\FlectModernMenu.cer'
    $package = Join-Path $repo 'build\modern\FlectModernMenu.msix'
    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new($publicCert)
    $signature = Get-AuthenticodeSignature -LiteralPath $package
    if ($cert.Subject -ne 'CN=Flect Local Menu' -or !$signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
        throw 'The package signer does not match the Flect local certificate.'
    }
    $trustedPath = 'Cert:\LocalMachine\TrustedPeople\' + $cert.Thumbprint
    $addedCertificate = !(Test-Path -LiteralPath $trustedPath)
    if ($addedCertificate) {
        Import-Certificate -FilePath $publicCert -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    }
    try {
        & (Join-Path $PSScriptRoot 'install.ps1') | Format-List | Out-String | Write-Output
    } catch {
        if ($addedCertificate) { Remove-Item -LiteralPath $trustedPath -Force }
        throw
    }
    Stop-Transcript | Out-Null
    exit 0
} catch {
    $_ | Format-List * -Force | Out-String | Write-Output
    Stop-Transcript | Out-Null
    exit 1
}
