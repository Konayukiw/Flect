<#
.SYNOPSIS
    Downloads the third-party binaries that ship inside the install folder.

.DESCRIPTION
    Dependencies which cannot be downloaded from NuGet:

      ffmpeg, ffprobe for video processing
      cfr.jar for java decompilation
      7za.exe for archive compression and extraction
      7z.exe + 7z.dll for RAR extraction (7za.exe cannot read RAR)

    ImageMagick and PDFium are not here because they arrive as NuGet packages
    (Magick.NET and PDFtoImage) and are already part of the build output.

    Everything lands in dependencies\, which build.ps1 stages into dist\deps\.
    Re-running skips whatever is already present.
#>

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $repoRoot 'dependencies'
New-Item -ItemType Directory -Force -Path $destination | Out-Null

$ffmpegUrl = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'
$cfrUrl = 'https://github.com/leibnitz27/cfr/releases/download/0.152/cfr-0.152.jar'

$sevenZipVersion = '26.02'
$sevenZipTag = $sevenZipVersion
$sevenZipFile = '2602'
$sevenZipStubUrl = "https://github.com/ip7z/7zip/releases/download/$sevenZipTag/7zr.exe"
$sevenZipExtraUrl = "https://github.com/ip7z/7zip/releases/download/$sevenZipTag/7z$sevenZipFile-extra.7z"
$sevenZipFullUrl = "https://github.com/ip7z/7zip/releases/download/$sevenZipTag/7z$sevenZipFile-x64.exe"

function Test-Present([string]$name) {
    $path = Join-Path $destination $name
    if ((Test-Path $path) -and -not $Force) {
        Write-Host "  $name already present"
        return $true
    }
    return $false
}

function Download-File([string]$uri, [string]$outFile) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-WebRequest -Uri $uri -OutFile $outFile -UseBasicParsing
            return
        }
        catch {
            if ($attempt -eq 3) { throw }
            Write-Host "  retrying $uri (attempt $attempt of 3)..."
            Start-Sleep -Seconds 2
        }
    }
}

Write-Host "Fetching items into $destination"

if (-not (Test-Present 'ffmpeg.exe') -or -not (Test-Present 'ffprobe.exe')) {
    $archive = Join-Path ([System.IO.Path]::GetTempPath()) 'ffmpeg-release-essentials.zip'
    $staging = Join-Path ([System.IO.Path]::GetTempPath()) 'ffmpeg-staging'

    Write-Host '  Downloading ffmpeg (about 110 MB)...'
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $archive -UseBasicParsing

    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
    Expand-Archive -Path $archive -DestinationPath $staging

    Get-ChildItem -Path $staging -Recurse -File |
        Where-Object { $_.Name -in @('ffmpeg.exe', 'ffprobe.exe') } |
        ForEach-Object {
            Copy-Item $_.FullName -Destination $destination -Force
            Write-Host ("  {0} ({1:N0} bytes)" -f $_.Name, $_.Length)
        }

    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
    Remove-Item -Force $archive -ErrorAction SilentlyContinue
}

if (-not (Test-Present 'cfr.jar')) {
    Write-Host '  Downloading cfr...'
    Invoke-WebRequest -Uri $cfrUrl -OutFile (Join-Path $destination 'cfr.jar') -UseBasicParsing
    Write-Host ("  cfr.jar ({0:N0} bytes)" -f (Get-Item (Join-Path $destination 'cfr.jar')).Length)
}

if (-not (Test-Present '7za.exe') -or -not (Test-Present '7z.exe') -or -not (Test-Present '7z.dll')) {
    $temp = [System.IO.Path]::GetTempPath()
    $stub = Join-Path $temp '7zr.exe'
    $extra = Join-Path $temp '7z-extra.7z'
    $installer = Join-Path $temp '7z-installer.exe'
    $staging = Join-Path $temp '7z-staging'

    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    Write-Host '  Downloading 7-Zip (26.02)...'
    Download-File $sevenZipStubUrl $stub
    Download-File $sevenZipExtraUrl $extra
    Download-File $sevenZipFullUrl $installer

    & $stub x $extra "-o$staging" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Extracting 7-Zip extra package failed.' }

    $sevenZa = Get-ChildItem -Path $staging -Recurse -File -Filter '7za.exe' |
        Where-Object { $_.DirectoryName -match '\\x64$' } | Select-Object -First 1
    if (-not $sevenZa) {
        $sevenZa = Get-ChildItem -Path $staging -Recurse -File -Filter '7za.exe' |
            Select-Object -First 1
    }
    if (-not $sevenZa) { throw '7za.exe was not found in the extra package.' }
    Copy-Item $sevenZa.FullName -Destination $destination -Force
    Write-Host ("  7za.exe ({0:N0} bytes)" -f $sevenZa.Length)

    & $sevenZa.FullName x $installer "-o$staging" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Extracting the 7-Zip installer failed.' }

    foreach ($name in @('7z.exe', '7z.dll')) {
        $file = Get-ChildItem -Path $staging -Recurse -File -Filter $name |
            Where-Object { $_.DirectoryName -match '\\x64$' } | Select-Object -First 1
        if (-not $file) {
            $file = Get-ChildItem -Path $staging -Recurse -File -Filter $name |
                Select-Object -First 1
        }
        if (-not $file) { throw "$name was not found in the installer package." }
        Copy-Item $file.FullName -Destination $destination -Force
        Write-Host ("  {0} ({1:N0} bytes)" -f $name, $file.Length)
    }

    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
    Remove-Item -Force $stub, $extra, $installer -ErrorAction SilentlyContinue
}

Write-Host 'Done.'
