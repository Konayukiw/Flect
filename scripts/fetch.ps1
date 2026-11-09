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

    Everything lands in dependencies\, which scripts\build.ps1 stages into dist\deps\.
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

$previewStaging = Join-Path $destination '.staging'
$previewRoot = Join-Path $destination 'preview\lib'
$systemTar = Join-Path $env:SystemRoot 'System32\tar.exe'

function Get-Verified([string]$uri, [string]$outFile, [string]$algorithm, [string]$expected) {
    Download-File $uri $outFile
    $digest = (Get-FileHash -LiteralPath $outFile -Algorithm $algorithm).Hash
    if ($digest -ne $expected) {
        throw "Hash mismatch for $uri. Expected $expected but got $digest."
    }
}

function Get-IntegrityHex([string]$integrity) {
    $bytes = [Convert]::FromBase64String($integrity.Substring($integrity.IndexOf('-') + 1))
    return ([BitConverter]::ToString($bytes)).Replace('-', '')
}

function Test-Stamp([string]$folder, [string]$version) {
    $stamp = Join-Path $folder '.version'
    return (-not $Force) -and (Test-Path -LiteralPath $stamp) -and ((Get-Content -LiteralPath $stamp -Raw).Trim() -eq $version)
}

function Set-Stamp([string]$folder, [string]$version) {
    Set-Content -LiteralPath (Join-Path $folder '.version') -Value $version -Encoding Ascii
}

function New-Staging([string]$name) {
    $path = Join-Path $previewStaging $name
    Remove-Item -Recurse -Force -LiteralPath $path -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    return $path
}

function Expand-Npm([string]$package, [string]$version, [string]$integrity, [string]$folder) {
    $archive = Join-Path $folder 'package.tgz'
    $leaf = $package.Substring($package.LastIndexOf('/') + 1)
    Get-Verified "https://registry.npmjs.org/$package/-/$leaf-$version.tgz" $archive 'SHA512' (Get-IntegrityHex $integrity)
    & $systemTar -xzf $archive -C $folder
    if ($LASTEXITCODE -ne 0) { throw "Extracting $package failed." }
    return (Join-Path $folder 'package')
}

function Reset-Folder([string]$path) {
    Remove-Item -Recurse -Force -LiteralPath $path -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

$webView2Version = '1.0.4314.35'
$webView2Hash = '90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4'
$webView2Folder = Join-Path $destination 'webview2'
if (-not (Test-Stamp $webView2Folder $webView2Version)) {
    Write-Host "  Downloading WebView2 SDK $webView2Version..."
    $staging = New-Staging 'webview2'
    $archive = Join-Path $staging 'sdk.zip'
    Get-Verified "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$webView2Version/microsoft.web.webview2.$webView2Version.nupkg" $archive 'SHA256' $webView2Hash
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $staging 'sdk')
    Reset-Folder $webView2Folder
    Copy-Item (Join-Path $staging 'sdk\build\native\include') -Destination $webView2Folder -Recurse
    New-Item -ItemType Directory -Force -Path (Join-Path $webView2Folder 'x64') | Out-Null
    Copy-Item (Join-Path $staging 'sdk\build\native\x64\WebView2LoaderStatic.lib') -Destination (Join-Path $webView2Folder 'x64')
    Copy-Item (Join-Path $staging 'sdk\LICENSE.txt') -Destination $webView2Folder
    Set-Stamp $webView2Folder $webView2Version
}

$monacoVersion = '0.57.0'
$monacoIntegrity = 'sha512-5BkI9KGoqrNvBGUe15/QlZq3OooZ8WLg1AxTpaqHRCP3HNpzPPZKE2EDz8M7c+VRmCeUw1Brp4cx/PWm3kI/5A=='
$monacoFolder = Join-Path $previewRoot 'monaco'
if (-not (Test-Stamp $monacoFolder $monacoVersion)) {
    Write-Host "  Downloading Monaco Editor $monacoVersion..."
    $staging = New-Staging 'monaco'
    $package = Expand-Npm 'monaco-editor' $monacoVersion $monacoIntegrity $staging
    Reset-Folder $monacoFolder
    Copy-Item (Join-Path $package 'min\vs') -Destination $monacoFolder -Recurse
    Copy-Item (Join-Path $package 'LICENSE') -Destination (Join-Path $monacoFolder 'LICENSE.txt')
    Copy-Item (Join-Path $package 'ThirdPartyNotices.txt') -Destination $monacoFolder -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath (Join-Path $monacoFolder 'vs\language') -Recurse -File -Filter '*.worker.js' | Remove-Item -Force
    Get-ChildItem -LiteralPath (Join-Path $monacoFolder 'vs\assets') -File |
        Where-Object { $_.Name -match '^(css|html|json|ts)\.worker-' } | Remove-Item -Force
    Remove-Item -Recurse -Force -LiteralPath (Join-Path $monacoFolder 'vs\nls')
    Set-Stamp $monacoFolder $monacoVersion
}

$pdfjsVersion = '6.5.438'
$pdfjsIntegrity = 'sha512-vg8Pw1x+AFKy4AMDvhuWl5cOYV8d698UoEANoFFhc79hqXDnZQqnOXoxw3HG907WHOg1JYOvLhP756boddFxpA=='
$pdfjsFolder = Join-Path $previewRoot 'pdfjs'
if (-not (Test-Stamp $pdfjsFolder $pdfjsVersion)) {
    Write-Host "  Downloading pdf.js $pdfjsVersion..."
    $staging = New-Staging 'pdfjs'
    $package = Expand-Npm 'pdfjs-dist' $pdfjsVersion $pdfjsIntegrity $staging
    Reset-Folder $pdfjsFolder
    foreach ($name in @('pdf.min.mjs', 'pdf.worker.min.mjs')) {
        Copy-Item (Join-Path $package "build\$name") -Destination $pdfjsFolder
    }
    Copy-Item (Join-Path $package 'cmaps') -Destination $pdfjsFolder -Recurse
    Copy-Item (Join-Path $package 'standard_fonts') -Destination $pdfjsFolder -Recurse
    if (Test-Path -LiteralPath (Join-Path $package 'wasm')) {
        Copy-Item (Join-Path $package 'wasm') -Destination $pdfjsFolder -Recurse
    }
    Copy-Item (Join-Path $package 'LICENSE') -Destination (Join-Path $pdfjsFolder 'LICENSE.txt')
    Set-Stamp $pdfjsFolder $pdfjsVersion
}

$markdownItVersion = '15.0.3'
$markdownItIntegrity = 'sha512-+Az7Ojz3iRGu8KYMvJ6xVhvrxP3eE+vEtJX75Ow88XBpcQNV/0lGX5ZynIIBftREo4DC2jsveAMDaeltmm8QvQ=='
$markdownItFolder = Join-Path $previewRoot 'markdown-it'
if (-not (Test-Stamp $markdownItFolder $markdownItVersion)) {
    Write-Host "  Downloading markdown-it $markdownItVersion..."
    $staging = New-Staging 'markdown-it'
    $package = Expand-Npm 'markdown-it' $markdownItVersion $markdownItIntegrity $staging
    Reset-Folder $markdownItFolder
    Copy-Item (Join-Path $package 'dist\browser\markdown-it.esm.min.mjs') -Destination (Join-Path $markdownItFolder 'markdown-it.min.mjs')
    Copy-Item (Join-Path $package 'LICENSE') -Destination (Join-Path $markdownItFolder 'LICENSE.txt')
    Set-Stamp $markdownItFolder $markdownItVersion
}

$hackGenVersion = '2.10.0'
$hackGenHash = 'ED182E2A4B95792D94DEA7932F6B45280B5AE353651BE249D5F6B7867B788DB7'
$hackGenLicenseHash = '9BFC3B3826A9C71042B87FDA90A19CDA4918894CDBD449CAB163B7A7C93C5AA7'
$fontFolder = Join-Path $previewRoot 'fonts'
if (-not (Test-Stamp $fontFolder $hackGenVersion)) {
    Write-Host "  Downloading HackGen $hackGenVersion..."
    $staging = New-Staging 'fonts'
    $archive = Join-Path $staging 'hackgen.zip'
    Get-Verified "https://github.com/yuru7/HackGen/releases/download/v$hackGenVersion/HackGen_v$hackGenVersion.zip" $archive 'SHA256' $hackGenHash
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $staging 'zip')
    $license = Join-Path $staging 'LICENSE.txt'
    Get-Verified "https://raw.githubusercontent.com/yuru7/HackGen/v$hackGenVersion/LICENSE" $license 'SHA256' $hackGenLicenseHash
    Reset-Folder $fontFolder
    foreach ($name in @('HackGenConsole-Regular.ttf', 'HackGenConsole-Bold.ttf')) {
        $file = Get-ChildItem -LiteralPath (Join-Path $staging 'zip') -Recurse -File -Filter $name | Select-Object -First 1
        if (-not $file) { throw "$name was not found in the HackGen package." }
        Copy-Item -LiteralPath $file.FullName -Destination $fontFolder
    }
    Copy-Item -LiteralPath $license -Destination (Join-Path $fontFolder 'LICENSE-HackGen.txt')
    Set-Stamp $fontFolder $hackGenVersion
}

Remove-Item -Recurse -Force -LiteralPath $previewStaging -ErrorAction SilentlyContinue

Write-Host 'Done.'
