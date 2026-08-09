<#
.SYNOPSIS
    Downloads the third-party binaries that ship inside the install folder.

.DESCRIPTION
    Dependencies which cannot be downloaded from NuGet:

      ffmpeg, ffprobe for video processing 
      cfr.jar for java decompilation

    ImageMagick and PDFium are not here because they arrive as NuGet packages
    (Magick.NET and PDFtoImage) and are already part of the build output.

    Everything lands in dependencies\, which build.ps1 stages into dist\tools\.
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

function Test-Present([string]$name) {
    $path = Join-Path $destination $name
    if ((Test-Path $path) -and -not $Force) {
        Write-Host "  $name already present"
        return $true
    }
    return $false
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

Write-Host 'Done.'
