[CmdletBinding()]
param(
    [string]$ShellDll,
    [switch]$InvokeWorker,
    [switch]$Registered
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props')
$brand = $props.Project.PropertyGroup | Where-Object { $_.BrandName } | Select-Object -First 1
$platform = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'ARM64' } else { 'x64' }
if (-not $ShellDll) {
    $relative = if ($platform -eq 'ARM64') { 'build\shell\ARM64\Release' } else { 'build\shell\Release' }
    $ShellDll = Join-Path $repoRoot "$relative\$($brand.BrandName)Shell.dll"
}
$ShellDll = (Resolve-Path -LiteralPath $ShellDll).Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio C++ build tools were not found.' }
& $msbuild (Join-Path $PSScriptRoot 'SmokeTest.vcxproj') /p:Configuration=Release /p:Platform=$platform /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'The native shell smoke test failed to build.' }

$fixtures = Join-Path $repoRoot ("build\shell\tests\fixtures-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures | Out-Null
$files = @((Join-Path $fixtures '日本語 space.json'), (Join-Path $fixtures 'second.json'))
foreach ($file in $files) { Set-Content -LiteralPath $file -Value '{"b":1,"a":[1,2]}' -Encoding utf8 }
$hashes = $files | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash }
$test = Join-Path $repoRoot "build\shell\tests\$platform\SmokeTest.exe"
$singleArguments = @($ShellDll, $brand.BrandClsid, $files[0])
if ($Registered) { $singleArguments += '--registered' }
& $test @singleArguments
if ($LASTEXITCODE -ne 0) { throw 'The native single-selection menu test failed.' }
$arguments = @($ShellDll, $brand.BrandClsid) + $files
if ($Registered) { $arguments += '--registered' }
if ($InvokeWorker) { $arguments += '--invoke' }
& $test @arguments
if ($LASTEXITCODE -ne 0) { throw 'The native multiple-selection menu test failed.' }

if ($InvokeWorker) {
    $outputs = $files | ForEach-Object { Join-Path $fixtures ([IO.Path]::GetFileNameWithoutExtension($_) + '_pretty.json') }
    $deadline = (Get-Date).AddSeconds(30)
    while (@($outputs | Where-Object { -not (Test-Path -LiteralPath $_) }).Count -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
    for ($i = 0; $i -lt $files.Count; $i++) {
        if (-not (Test-Path -LiteralPath $outputs[$i])) { throw "The worker did not create $($outputs[$i])." }
        $json = Get-Content -LiteralPath $outputs[$i] -Raw | ConvertFrom-Json
        if ($json.b -ne 1 -or $json.a.Count -ne 2) { throw 'The worker output is incorrect.' }
        if ((Get-FileHash -LiteralPath $files[$i]).Hash -ne $hashes[$i]) { throw 'An original file was modified.' }
    }
    Write-Host 'PASS native menu launched the x64 worker; both outputs are correct and originals are unchanged'
}
Write-Host "All $platform classic menu smoke tests passed. Fixtures: $fixtures"
