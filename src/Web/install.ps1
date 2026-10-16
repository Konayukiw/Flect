param(
    [string]$Version,
    [switch]$Uninstall
)

& {
    param([string]$Version, [bool]$Uninstall)

    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    $brand = 'Flect'
    $repository = 'Konayukiw/Flect'
    $appId = '{16C090EA-F07E-47B7-8811-B97D3BE26DDC}'
    $clsid = '{4D038D4F-2125-48BE-8848-BA30A13AC7EA}'
    $api = "https://api.github.com/repos/$repository"
    $releasesPage = "https://github.com/$repository/releases"
    $runtimeMajor = '10'
    $runtimePackage = 'Microsoft.DotNet.DesktopRuntime.10'
    $runtimeUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe'
    $runtimePage = 'https://dotnet.microsoft.com/download/dotnet/10.0'
    $registrationFailedCode = 100
    $uninstallPath = "Software\Microsoft\Windows\CurrentVersion\Uninstall\${appId}_is1"
    $serverPath = "Software\Classes\CLSID\$clsid\InprocServer32"

    function Write-Step([string]$Message) {
        Write-Host "==> $Message" -ForegroundColor Cyan
    }

    function Get-UserValue([string]$Path, [string]$Name) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
        try {
            $key = $root.OpenSubKey($Path)
            if ($key) {
                try { $key.GetValue($Name) } finally { $key.Dispose() }
            }
        }
        finally {
            $root.Dispose()
        }
    }

    function Invoke-GitHub([string]$Uri) {
        try {
            Invoke-RestMethod -Uri $Uri -UseBasicParsing -Headers @{ 'Accept' = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
        }
        catch {
            $response = $_.Exception.Response
            $status = if ($response) { [int]$response.StatusCode } else { 0 }
            if ($status -eq 404) { return $null }
            if ($status -eq 403 -or $status -eq 429) {
                throw "GitHub refused the request (HTTP $status), most likely because of its rate limit. Wait a while, then run this again."
            }
            throw "Could not reach GitHub: $($_.Exception.Message)"
        }
    }

    function Find-Release {
        if (-not $Version) {
            $latest = Invoke-GitHub "$api/releases/latest"
            if (-not $latest) { throw "No $brand release has been published yet. See $releasesPage" }
            return $latest
        }

        $wanted = $Version.Trim().TrimStart('v', 'V')
        for ($page = 1; $page -le 10; $page++) {
            $releases = Invoke-GitHub "$api/releases?per_page=100&page=$page"
            if (-not $releases) { break }
            foreach ($release in $releases) {
                if (-not $release.draft -and "$($release.tag_name)".TrimStart('v', 'V') -eq $wanted) { return $release }
            }
        }
        throw "$brand $Version was not found. See $releasesPage"
    }

    function Find-Setup($Release) {
        $pattern = '^' + [regex]::Escape($brand) + '-.*setup\.exe$'
        $asset = $Release.assets | Where-Object { $_.name -match $pattern } | Select-Object -First 1
        if (-not $asset) { throw "$brand $($Release.tag_name) has no Setup file attached. See $releasesPage" }
        $asset
    }

    function Save-File([string]$Uri, [string]$Path) {
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $Path -UseBasicParsing
        }
        catch {
            throw "Could not download $Uri ($($_.Exception.Message))"
        }
    }

    function Assert-Digest($Asset, [string]$Path) {
        $published = if ($Asset.PSObject.Properties['digest']) { "$($Asset.digest)" } else { '' }
        if ($published -notmatch '^sha256:([0-9a-fA-F]{64})$') {
            Write-Warning "GitHub has no SHA256 digest for $($Asset.name), so the download could not be verified."
            return
        }
        $expected = $Matches[1]
        $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        if ($actual -ne $expected) {
            throw "The SHA256 of $($Asset.name) does not match the one GitHub published, so it was not run.`n  expected $($expected.ToLowerInvariant())`n  actual   $($actual.ToLowerInvariant())"
        }
        Write-Host "    SHA256 $($actual.ToLowerInvariant()) verified"
    }

    function Test-Runtime {
        $programs = if (${env:ProgramW6432}) { ${env:ProgramW6432} } else { $env:ProgramFiles }
        $root = Join-Path $programs 'dotnet\shared\Microsoft.WindowsDesktop.App'
        [bool](Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$runtimeMajor.*" })
    }

    function Confirm-Runtime {
        Write-Host "$brand needs the .NET $runtimeMajor Desktop Runtime (x64), which is not installed." -ForegroundColor Yellow
        $answer = $null
        if (-not [Console]::IsInputRedirected) {
            try { $answer = Read-Host 'Install it now? Windows will ask for administrator permission. [Y/n]' } catch { $answer = $null }
        }
        if ($null -eq $answer) {
            throw "Install the .NET $runtimeMajor Desktop Runtime from $runtimePage, then run this again."
        }
        if ($answer.Trim() -match '^(y|yes)?$') { return $true }
        Write-Host "$brand was not installed. Install the runtime from $runtimePage, then run this again." -ForegroundColor Yellow
        $false
    }

    function Install-Runtime([string]$Folder) {
        $winget = Get-Command winget.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($winget) {
            Write-Step "Installing the .NET $runtimeMajor Desktop Runtime with winget"
            & $winget.Source install --id $runtimePackage --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
            if (Test-Runtime) { return }
            Write-Warning "winget did not install the runtime (exit code $LASTEXITCODE), so it will be downloaded from Microsoft instead."
        }

        Write-Step "Downloading the .NET $runtimeMajor Desktop Runtime from Microsoft (about 60 MB)"
        $installer = Join-Path $Folder 'windowsdesktop-runtime-win-x64.exe'
        Save-File $runtimeUrl $installer
        $signature = Get-AuthenticodeSignature -LiteralPath $installer
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
            throw "The downloaded runtime installer is not signed by Microsoft, so it was not run."
        }

        Write-Step "Installing the .NET $runtimeMajor Desktop Runtime"
        try {
            Start-Process -FilePath $installer -ArgumentList '/install /quiet /norestart' -Verb RunAs -Wait | Out-Null
        }
        catch {
            throw "The runtime was not installed because administrator permission was not granted."
        }
        if (-not (Test-Runtime)) {
            throw "The runtime installer did not finish. Install it from $runtimePage, then run this again."
        }
    }

    function Install-App([string]$Folder) {
        if (-not (Test-Runtime)) {
            if (-not (Confirm-Runtime)) { return }
            Install-Runtime $Folder
        }

        Write-Step $(if ($Version) { "Looking up $brand $Version" } else { "Looking up the latest $brand release" })
        $release = Find-Release
        $asset = Find-Setup $release
        $setup = Join-Path $Folder $asset.name

        Write-Step ('Downloading {0} ({1:N1} MB)' -f $asset.name, ($asset.size / 1MB))
        Save-File $asset.browser_download_url $setup
        Assert-Digest $asset $setup

        Write-Step "Installing $brand $($release.tag_name)"
        Write-Host '    Explorer may restart while an older version is replaced.'
        $log = Join-Path $Folder 'setup.log'
        $process = Start-Process -FilePath $setup -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$log`"" -Wait -PassThru
        $code = $process.ExitCode
        if ($code -eq 7) {
            throw "The Explorer menu of the installed $brand is still in use, so it could not be replaced. Sign out and back in, then run this again."
        }
        if ($code -eq $registrationFailedCode) {
            throw "The files were installed, but registering the Explorer menu failed. Run this again, and if it keeps failing, see $log"
        }
        if ($code -ne 0) {
            throw "Setup failed with exit code $code. See $log"
        }

        $server = Get-UserValue $serverPath ''
        if (-not $server -or -not (Test-Path -LiteralPath $server)) {
            throw "Setup finished, but the Explorer menu is not registered. See $log"
        }

        Write-Host ''
        Write-Host "$brand $(Get-UserValue $uninstallPath 'DisplayVersion') is installed." -ForegroundColor Green
        Write-Host "Right-click a file or folder and choose ""Show more options"" to find $brand."
        Write-Host "Open $brand from the Start menu to change its settings."
    }

    function Uninstall-App {
        $uninstaller = "$(Get-UserValue $uninstallPath 'UninstallString')".Trim().Trim('"')
        if (-not $uninstaller) {
            Write-Host "$brand is not installed."
            return
        }
        if (-not (Test-Path -LiteralPath $uninstaller)) {
            throw "The $brand uninstaller is missing ($uninstaller). Install $brand again, then uninstall it."
        }

        $location = Get-UserValue $uninstallPath 'InstallLocation'
        Write-Step "Uninstalling $brand $(Get-UserValue $uninstallPath 'DisplayVersion')"
        Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait

        $deadline = (Get-Date).AddMinutes(2)
        while ((Get-UserValue $uninstallPath 'UninstallString') -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
        if (Get-UserValue $uninstallPath 'UninstallString') {
            throw "The uninstaller did not finish within two minutes."
        }
        while ((Test-Path -LiteralPath $uninstaller) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }

        Write-Host ''
        Write-Host "$brand is uninstalled." -ForegroundColor Green
        $kept = @($location, (Join-Path $env:APPDATA $brand)) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
        if ($kept) {
            Write-Host 'Your settings and downloaded AI components are kept. Delete these folders to remove them too:'
            foreach ($folder in $kept) { Write-Host "    $folder" }
        }
    }

    if ($Uninstall -and $Version) { throw 'Use either -Version or -Uninstall, not both.' }
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw "$brand runs on Windows only." }
    if (-not [Environment]::Is64BitOperatingSystem) { throw "$brand needs 64-bit Windows." }
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    if ($Uninstall) {
        Uninstall-App
        return
    }

    $work = Join-Path ([IO.Path]::GetTempPath()) ("$brand-install-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $failed = $true
    try {
        Install-App $work
        $failed = $false
    }
    finally {
        if ($failed -and (Test-Path -LiteralPath (Join-Path $work 'setup.log'))) {
            Get-ChildItem -LiteralPath $work | Where-Object { $_.Name -ne 'setup.log' } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
        else {
            Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} $Version ([bool]$Uninstall)
