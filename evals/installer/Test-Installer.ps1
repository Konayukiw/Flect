[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Installer,

    [string]$LogDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) 'flect-installer-logs')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

[xml]$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
$group = $props.Project.PropertyGroup | Where-Object { $_.BrandName } | Select-Object -First 1
$brand = $group.BrandName
$clsid = $group.BrandClsid
$appId = $group.BrandAppId.Trim('{', '}')
$version = $group.BrandVersion

$app = Join-Path $env:LOCALAPPDATA $brand
$classes = 'HKCU:\Software\Classes'
$clsidKey = "$classes\CLSID\$clsid"
$handlerKeys = @("$classes\*\shellex\ContextMenuHandlers\$brand", "$classes\Directory\shellex\ContextMenuHandlers\$brand")
$presetKey = "HKCU:\Software\$brand\Presets"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$appId}_is1"

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

function Get-DefaultValue([string]$Key) {
    if (-not (Test-Path -LiteralPath $Key)) { return $null }
    return (Get-Item -LiteralPath $Key).GetValue('')
}

function Invoke-Setup([string]$Name) {
    $log = Join-Path $LogDirectory "$Name.log"
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"")
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru
    Assert-That ($process.ExitCode -eq 0) "$Name finished with exit code 0 (got $($process.ExitCode))"
}

function Assert-Registered([string]$Stage) {
    $server = Get-DefaultValue "$clsidKey\InprocServer32"
    Assert-That ($server -and ($server -ieq (Join-Path $app "${brand}Shell.dll"))) "$Stage - CLSID points at the installed shell extension ($server)"
    $threading = if (Test-Path -LiteralPath "$clsidKey\InprocServer32") {
        (Get-Item -LiteralPath "$clsidKey\InprocServer32").GetValue('ThreadingModel')
    }
    Assert-That ($threading -eq 'Apartment') "$Stage - ThreadingModel is Apartment ($threading)"
    foreach ($key in $handlerKeys) {
        Assert-That ((Get-DefaultValue $key) -eq $clsid) "$Stage - $key is registered"
    }
}

$runtime = Join-Path ${env:ProgramW6432} 'dotnet\shared\Microsoft.WindowsDesktop.App'
$hasRuntime = (Test-Path $runtime) -and (Get-ChildItem $runtime -Directory | Where-Object Name -like '10.*')
Assert-That ([bool]$hasRuntime) '.NET 10 Desktop Runtime is present for Setup'
if (-not $hasRuntime) { exit 1 }

New-Item -Path $presetKey -Force | Out-Null
New-ItemProperty -Path $presetKey -Name 'Video.CompressDiscord' -Value '10485760' -PropertyType String -Force | Out-Null

Invoke-Setup 'install'

foreach ($file in @("$brand.exe", "${brand}Shell.dll", 'unins000.exe', 'deps\ffmpeg.exe', 'deps\ffprobe.exe',
                    'deps\7za.exe', 'deps\7z.exe', 'deps\cfr.jar', 'deps\subtitles.py', 'deps\rembg.py')) {
    Assert-That (Test-Path (Join-Path $app $file)) "install - $file is installed"
}
Assert-Registered 'install'
Assert-That ((Get-ItemProperty -Path $presetKey).'Video.CompressDiscord' -eq '20971520') 'install - legacy Discord preset was raised to 20 MiB'
Assert-That ((Get-ItemProperty -Path $uninstallKey -ErrorAction SilentlyContinue).DisplayVersion -eq $version) "install - uninstall entry reports $version"

$smoke = Join-Path ([System.IO.Path]::GetTempPath()) "flect-smoke-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $smoke | Out-Null
$json = Join-Path $smoke 'data.json'
Set-Content -Path $json -Value '{"b":1,"a":[1,2]}' -Encoding utf8
$selection = Join-Path $smoke 'selection.txt'
Set-Content -Path $selection -Value $json -Encoding utf8
$worker = Start-Process -FilePath (Join-Path $app "$brand.exe") -ArgumentList @('--task', 'json.pretty', '--input', "`"$selection`"") -PassThru
if (-not $worker.WaitForExit(120000)) {
    Stop-Process -Id $worker.Id -Force
    Assert-That $false 'smoke - the installed worker finished within two minutes'
}
Assert-That (Test-Path (Join-Path $smoke 'data_pretty.json')) 'smoke - the installed worker pretty-printed a JSON file'
Remove-Item -Recurse -Force $smoke -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path (Join-Path $app 'env'), (Join-Path $app 'models') | Out-Null
Set-Content -Path (Join-Path $app 'env\marker.txt') -Value 'env'
Set-Content -Path (Join-Path $app 'models\marker.txt') -Value 'models'
Set-Content -Path (Join-Path $app 'stale.txt') -Value 'left over from an older version'

Invoke-Setup 'upgrade'

Assert-That (Test-Path (Join-Path $app 'env\marker.txt')) 'upgrade - the Python environment survives'
Assert-That (Test-Path (Join-Path $app 'models\marker.txt')) 'upgrade - downloaded models survive'
Assert-That (-not (Test-Path (Join-Path $app 'stale.txt'))) 'upgrade - files from the previous version are cleared'
Assert-That (Test-Path (Join-Path $app "$brand.exe")) 'upgrade - the worker is installed again'
Assert-Registered 'upgrade'

$uninstaller = Join-Path $app 'unins000.exe'
Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$(Join-Path $LogDirectory 'uninstall.log')`"") -Wait
$deadline = (Get-Date).AddMinutes(2)
while ((Test-Path $uninstallKey) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
while ((Test-Path $uninstaller) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }

Assert-That (-not (Test-Path $uninstallKey)) 'uninstall - the uninstall entry is removed'
Assert-That (-not (Test-Path -LiteralPath $clsidKey)) 'uninstall - the CLSID registration is removed'
foreach ($key in $handlerKeys) {
    Assert-That (-not (Test-Path -LiteralPath $key)) "uninstall - $key is removed"
}
Assert-That (-not (Test-Path (Join-Path $app "$brand.exe"))) 'uninstall - the worker is removed'
Assert-That (-not (Test-Path (Join-Path $app "${brand}Shell.dll"))) 'uninstall - the shell extension is removed'

$leftovers = Get-ChildItem -Path $app -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName.Substring($app.Length + 1) }
Write-Host "INFO  left in $app after uninstall: $(if ($leftovers) { $leftovers -join ', ' } else { 'nothing' })"

if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed" -ForegroundColor Red
    exit 1
}
Write-Host 'All installer checks passed'
