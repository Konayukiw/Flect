[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Installer,

    [string]$LogDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) 'flect-installer-logs')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script = Join-Path $repoRoot 'src\Web\install.ps1'
$server = Join-Path $PSScriptRoot 'dummy.mjs'

[xml]$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
$group = $props.Project.PropertyGroup | Where-Object { $_.BrandName } | Select-Object -First 1
$brand = $group.BrandName
$clsid = $group.BrandClsid
$appId = $group.BrandAppId
$version = $group.BrandVersion

$app = Join-Path $env:LOCALAPPDATA $brand
$clsidKey = "HKCU:\Software\Classes\CLSID\$clsid"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($appId)_is1"

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$script:failures = 0

function Assert-That([bool]$Condition, [string]$Message) {
    if ($Condition) {
        Write-Host "PASS  $Message"
    }
    else {
        Write-Host "FAIL  $Message" -ForegroundColor Red
        $script:failures++
    }
}

function Start-FakeGitHub([string]$Mode) {
    $out = Join-Path $LogDirectory "dummy-$Mode.out"
    $arguments = @($server, '--script', $script, '--setup', $Installer, '--tag', "v$version", '--older', 'v0.0.1',
                   '--mode', $Mode, '--log', (Join-Path $LogDirectory "dummy-$Mode.log"))
    $process = Start-Process -FilePath 'node' -ArgumentList ($arguments | ForEach-Object { "`"$_`"" }) -RedirectStandardOutput $out -PassThru -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds(15)
    $port = $null
    while (-not $port -and (Get-Date) -lt $deadline) {
        if ((Test-Path $out) -and ((Get-Content $out -Raw) -match 'listening (\d+)')) { $port = $Matches[1] }
        else { Start-Sleep -Milliseconds 200 }
    }
    if (-not $port) { throw "The fake GitHub server did not start ($Mode)." }
    [pscustomobject]@{ Process = $process; Url = "http://127.0.0.1:$port/install.ps1" }
}

function Invoke-InstallScript([string]$Shell, [string]$Url, [string]$Arguments, [string]$Name) {
    $command = if ($Arguments) { "& ([scriptblock]::Create((irm '$Url'))) $Arguments" } else { "irm '$Url' | iex" }
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & $Shell -NoProfile -NonInteractive -Command $command 2>&1 | Out-String
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previous
    Set-Content -Path (Join-Path $LogDirectory "install-script-$Name.txt") -Value $output
    Write-Host ($output.TrimEnd() -replace '(?m)^', "      ")
    [pscustomobject]@{ Code = $code; Output = $output }
}

function Get-DefaultValue([string]$Key) {
    if (-not (Test-Path -LiteralPath $Key)) { return $null }
    return (Get-Item -LiteralPath $Key).GetValue('')
}

$text = Get-Content -Raw -Path $script
Assert-That ($text -match [regex]::Escape("`$brand = '$brand'")) "install.ps1 uses the brand name $brand"
Assert-That ($text -match [regex]::Escape("`$appId = '$appId'")) "install.ps1 uses the AppId $appId"
Assert-That ($text -match [regex]::Escape("`$clsid = '$clsid'")) "install.ps1 uses the CLSID $clsid"
Assert-That (-not (Test-Path $uninstallKey)) "$brand is not installed before the install.ps1 checks"

$fake = Start-FakeGitHub 'bad-digest'
try {
    $run = Invoke-InstallScript 'powershell.exe' $fake.Url '' 'bad-digest'
    Assert-That ($run.Code -ne 0) "bad digest - install.ps1 fails (exit $($run.Code))"
    Assert-That ($run.Output -match 'does not match the one GitHub published') 'bad digest - the SHA256 mismatch is reported'
    Assert-That (-not (Test-Path $uninstallKey)) 'bad digest - Setup was not run'
}
finally {
    Stop-Process -Id $fake.Process.Id -Force -ErrorAction SilentlyContinue
}

$fake = Start-FakeGitHub 'normal'
try {
    $run = Invoke-InstallScript 'powershell.exe' $fake.Url '' 'install'
    Assert-That ($run.Code -eq 0) "irm | iex in Windows PowerShell installs $brand (exit $($run.Code))"
    Assert-That ($run.Output -match 'SHA256 [0-9a-f]{64} verified') 'install - the SHA256 digest is verified'
    Assert-That ((Get-DefaultValue "$clsidKey\InprocServer32") -ieq (Join-Path $app "${brand}Shell.dll")) 'install - the Explorer menu is registered'
    Assert-That ((Get-ItemProperty -Path $uninstallKey -ErrorAction SilentlyContinue).DisplayVersion -eq $version) "install - the uninstall entry reports $version"

    $run = Invoke-InstallScript 'pwsh' $fake.Url '-Version 0.0.1' 'version'
    Assert-That ($run.Code -eq 0) "-Version in PowerShell 7 installs over the existing copy (exit $($run.Code))"
    Assert-That ((Get-Content -Raw (Join-Path $LogDirectory 'dummy-normal.log')) -match 'GET /download/v0\.0\.1/') '-Version - the requested release is downloaded'
    Assert-That ((Get-DefaultValue "$clsidKey\InprocServer32") -ieq (Join-Path $app "${brand}Shell.dll")) '-Version - the Explorer menu is still registered'

    $run = Invoke-InstallScript 'pwsh' $fake.Url '-Uninstall' 'uninstall'
    Assert-That ($run.Code -eq 0) "-Uninstall removes $brand (exit $($run.Code))"
    Assert-That (-not (Test-Path $uninstallKey)) '-Uninstall - the uninstall entry is removed'
    Assert-That (-not (Test-Path -LiteralPath $clsidKey)) '-Uninstall - the CLSID registration is removed'
    Assert-That (-not (Test-Path (Join-Path $app "${brand}Shell.dll"))) '-Uninstall - the shell extension is removed'

    $run = Invoke-InstallScript 'powershell.exe' $fake.Url '-Uninstall' 'uninstall-again'
    Assert-That ($run.Code -eq 0 -and $run.Output -match "$brand is not installed") '-Uninstall - running it again is harmless'
}
finally {
    Stop-Process -Id $fake.Process.Id -Force -ErrorAction SilentlyContinue
}

if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed" -ForegroundColor Red
    exit 1
}
Write-Host 'All install.ps1 checks passed'
