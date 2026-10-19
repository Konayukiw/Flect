param([switch]$InvokeWorker, [switch]$Packaged)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$repo = $ModernMenuRepo
Build-ModernMenu -WithTests
$work = Join-Path $repo ('build\modern\smoke-' + [guid]::NewGuid().ToString('N'))
$arguments = @($work)
if ($InvokeWorker) { $arguments += '--invoke' }
if ($Packaged) { $arguments += '--packaged' }
& (Join-Path $repo 'build\modern\bin\FlectMenuSmokeTest.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Modern menu smoke tests failed.' }
if ($InvokeWorker) {
    $inputFile = Get-ChildItem -LiteralPath $work -Filter '* test.json' -File | Select-Object -First 1
    $output = Join-Path $work ($inputFile.BaseName + '_pretty.json')
    $deadline = (Get-Date).AddSeconds(10)
    while (!(Test-Path -LiteralPath $output) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (!(Test-Path -LiteralPath $output)) { throw 'The worker did not produce formatted JSON.' }
    $formatted = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    if ($formatted.z -ne 1 -or $formatted.a -ne 2) { throw 'The worker output did not preserve the input values.' }
    $original = Get-Content -LiteralPath $inputFile.FullName -Raw
    if ($original -ne '{"z":1,"a":2}') { throw 'The original JSON file was modified.' }
    Write-Output 'PASS worker produced formatted JSON and preserved the original'
}
