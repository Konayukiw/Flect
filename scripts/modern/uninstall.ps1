param([switch]$RemoveLocalCertificate, [switch]$Elevated)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
if ($RemoveLocalCertificate) {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        if ($Elevated) { throw 'Administrator privileges were not granted.' }
        $child = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
            -Verb RunAs -WindowStyle Hidden -PassThru `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-RemoveLocalCertificate', '-Elevated')
        $child.WaitForExit()
        exit $child.ExitCode
    }
}
Get-AppxPackage -Name (Get-ModernMenuPackageName) | Remove-AppxPackage -ErrorAction Stop
if ($RemoveLocalCertificate) {
    $repo = $ModernMenuRepo
    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $repo 'build\modern\FlectModernMenu.cer'))
    if ($cert.Subject -ne 'CN=Flect Local Menu') { throw 'Unexpected signing certificate.' }
    $path = 'Cert:\LocalMachine\TrustedPeople\' + $cert.Thumbprint
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
Write-Output 'Flect modern menu removed. Flect itself is still installed.'
