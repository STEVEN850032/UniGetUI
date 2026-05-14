[CmdletBinding()]
param(
    [string] $ReleaseRepository = 'Devolutions/UniGetUI',

    [string] $ReleaseTag = '',

    [string] $InstallerAssetName = 'UniGetUI.Installer.x64.exe',

    [string] $ArtifactsDir = (Join-Path $env:GITHUB_WORKSPACE 'artifacts\release-smoke'),

    [string] $GitHubToken = $env:GITHUB_TOKEN
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-GitHubApi {
    param(
        [Parameter(Mandatory)]
        [string] $Uri
    )

    $headers = @{
        Accept = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }

    if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
        $headers['Authorization'] = "Bearer $GitHubToken"
    }

    Invoke-RestMethod -Uri $Uri -Headers $headers
}

function Download-ReleaseAsset {
    param(
        [Parameter(Mandatory)]
        [string] $Uri,

        [Parameter(Mandatory)]
        [string] $DestinationPath
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
        $headers['Authorization'] = "Bearer $GitHubToken"
    }

    Invoke-WebRequest -Uri $Uri -Headers $headers -OutFile $DestinationPath
}

function Initialize-ScreenshotSupport {
    if (-not ('ReleaseSmokeNativeMethods' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class ReleaseSmokeNativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rasterOperation);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
}
'@
    }

    [ReleaseSmokeNativeMethods]::SetProcessDPIAware() | Out-Null

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
}

function Get-PhysicalDesktopBounds {
    Initialize-ScreenshotSupport

    $sourceDc = [ReleaseSmokeNativeMethods]::GetDC([IntPtr]::Zero)
    if ($sourceDc -eq [IntPtr]::Zero) {
        throw 'GetDC failed while reading desktop dimensions.'
    }

    try {
        $desktopHorzRes = 118
        $desktopVertRes = 117
        $horzRes = 8
        $vertRes = 10

        $width = [ReleaseSmokeNativeMethods]::GetDeviceCaps($sourceDc, $desktopHorzRes)
        $height = [ReleaseSmokeNativeMethods]::GetDeviceCaps($sourceDc, $desktopVertRes)

        if ($width -le 0 -or $height -le 0) {
            $width = [ReleaseSmokeNativeMethods]::GetDeviceCaps($sourceDc, $horzRes)
            $height = [ReleaseSmokeNativeMethods]::GetDeviceCaps($sourceDc, $vertRes)
        }

        if ($width -le 0 -or $height -le 0) {
            throw "Desktop device dimensions are invalid: width=$width, height=$height."
        }

        [pscustomobject]@{
            Left = 0
            Top = 0
            Width = $width
            Height = $height
        }
    }
    finally {
        [ReleaseSmokeNativeMethods]::ReleaseDC([IntPtr]::Zero, $sourceDc) | Out-Null
    }
}

function Wait-UniGetUIWindow {
    param(
        [Parameter(Mandatory)]
        [int] $SessionId,

        [int] $TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $process = Get-Process -Name UniGetUI -ErrorAction SilentlyContinue |
            Where-Object { $_.SessionId -eq $SessionId -and $_.MainWindowHandle -ne [IntPtr]::Zero } |
            Sort-Object -Property StartTime -Descending |
            Select-Object -First 1

        if ($null -ne $process) {
            return $process
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for a UniGetUI window in interactive session $SessionId."
}

function Save-DesktopScreenshot {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    Initialize-ScreenshotSupport

    $directory = Split-Path -Path $Path -Parent
    New-Item -Path $directory -ItemType Directory -Force | Out-Null

    $bounds = Get-PhysicalDesktopBounds
    $bitmap = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $sourceDc = [ReleaseSmokeNativeMethods]::GetDC([IntPtr]::Zero)
        if ($sourceDc -eq [IntPtr]::Zero) {
            throw 'GetDC failed while capturing the desktop.'
        }

        $targetDc = $graphics.GetHdc()
        try {
            $srccopy = 0x00CC0020
            if (-not [ReleaseSmokeNativeMethods]::BitBlt($targetDc, 0, 0, $bounds.Width, $bounds.Height, $sourceDc, $bounds.Left, $bounds.Top, $srccopy)) {
                throw "BitBlt failed while capturing the desktop."
            }
        }
        finally {
            $graphics.ReleaseHdc($targetDc)
            [ReleaseSmokeNativeMethods]::ReleaseDC([IntPtr]::Zero, $sourceDc) | Out-Null
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Save-WindowScreenshot {
    param(
        [Parameter(Mandatory)]
        [IntPtr] $WindowHandle,

        [Parameter(Mandatory)]
        [string] $Path
    )

    Initialize-ScreenshotSupport

    $directory = Split-Path -Path $Path -Parent
    New-Item -Path $directory -ItemType Directory -Force | Out-Null

    [ReleaseSmokeNativeMethods]::ShowWindow($WindowHandle, 9) | Out-Null
    [ReleaseSmokeNativeMethods]::SetForegroundWindow($WindowHandle) | Out-Null
    Start-Sleep -Seconds 3

    $rect = New-Object 'ReleaseSmokeNativeMethods+RECT'
    if (-not [ReleaseSmokeNativeMethods]::GetWindowRect($WindowHandle, [ref] $rect)) {
        throw "Could not read UniGetUI window bounds for handle $WindowHandle."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "UniGetUI window bounds are invalid: left=$($rect.Left), top=$($rect.Top), right=$($rect.Right), bottom=$($rect.Bottom)."
    }

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $targetDc = $graphics.GetHdc()
        try {
            if (-not [ReleaseSmokeNativeMethods]::PrintWindow($WindowHandle, $targetDc, 2)) {
                $sourceDc = [ReleaseSmokeNativeMethods]::GetDC([IntPtr]::Zero)
                if ($sourceDc -eq [IntPtr]::Zero) {
                    throw "GetDC failed while capturing the UniGetUI window."
                }

                try {
                    $srccopy = 0x00CC0020
                    if (-not [ReleaseSmokeNativeMethods]::BitBlt($targetDc, 0, 0, $width, $height, $sourceDc, $rect.Left, $rect.Top, $srccopy)) {
                        throw "BitBlt failed while capturing the UniGetUI window."
                    }
                }
                finally {
                    [ReleaseSmokeNativeMethods]::ReleaseDC([IntPtr]::Zero, $sourceDc) | Out-Null
                }
            }
        }
        finally {
            $graphics.ReleaseHdc($targetDc)
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-Item -Path $ArtifactsDir -ItemType Directory -Force | Out-Null
$downloadsDir = Join-Path $ArtifactsDir 'downloads'
New-Item -Path $downloadsDir -ItemType Directory -Force | Out-Null

$escapedRepository = $ReleaseRepository.Trim('/')
if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
    $release = Invoke-GitHubApi -Uri "https://api.github.com/repos/$escapedRepository/releases/latest"
}
else {
    $release = Invoke-GitHubApi -Uri "https://api.github.com/repos/$escapedRepository/releases/tags/$([System.Uri]::EscapeDataString($ReleaseTag))"
}

$installerAsset = $release.assets | Where-Object { $_.name -eq $InstallerAssetName } | Select-Object -First 1
if (-not $installerAsset) {
    throw "Could not find installer asset '$InstallerAssetName' in release $($release.tag_name)."
}

$checksumsAsset = $release.assets | Where-Object { $_.name -eq 'checksums.txt' } | Select-Object -First 1
if (-not $checksumsAsset) {
    throw "Could not find checksums.txt in release $($release.tag_name)."
}

$installerPath = Join-Path $downloadsDir $installerAsset.name
$checksumsPath = Join-Path $downloadsDir $checksumsAsset.name
Download-ReleaseAsset -Uri $installerAsset.browser_download_url -DestinationPath $installerPath
Download-ReleaseAsset -Uri $checksumsAsset.browser_download_url -DestinationPath $checksumsPath

$checksumPattern = "^(?<hash>[A-Fa-f0-9]{64})\s+$([regex]::Escape($installerAsset.name))$"
$checksumLine = Select-String -Path $checksumsPath -Pattern $checksumPattern | Select-Object -First 1
if (-not $checksumLine) {
    throw "Could not find SHA256 for '$($installerAsset.name)' in $checksumsPath."
}

$expectedHash = $checksumLine.Matches[0].Groups['hash'].Value.ToUpperInvariant()
$actualHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "SHA256 mismatch for $installerPath. Expected $expectedHash but got $actualHash."
}

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\UniGetUI'
$installerLogPath = Join-Path $ArtifactsDir 'unigetui-installer.log'
$installArguments = @(
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/NORESTART',
    '/CURRENTUSER',
    '/TASKS=regularinstall',
    "/DIR=$installDir",
    '/NoAutoStart',
    '/NoRunOnStartup',
    "/LOG=$installerLogPath"
)

$installerProcess = Start-Process -FilePath $installerPath -ArgumentList $installArguments -Wait -PassThru
if ($installerProcess.ExitCode -notin @(0, 3010)) {
    throw "UniGetUI installer failed with exit code $($installerProcess.ExitCode). See $installerLogPath."
}

$exeCandidates = @(
    (Join-Path $installDir 'UniGetUI.exe'),
    (Join-Path $env:ProgramFiles 'UniGetUI\UniGetUI.exe')
)

$installedExe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $installedExe) {
    throw "Could not locate installed UniGetUI.exe. Checked: $($exeCandidates -join ', ')"
}

$launchProcess = Start-Process -FilePath $installedExe -PassThru
Start-Sleep -Seconds 20

$currentSessionId = (Get-Process -Id $PID).SessionId
$screenshotsDir = Join-Path $ArtifactsDir 'screenshots'
$desktopScreenshotPath = Join-Path $screenshotsDir 'unigetui-desktop.png'
$windowScreenshotPath = Join-Path $screenshotsDir 'unigetui-window.png'
$failureScreenshotPath = Join-Path $screenshotsDir 'unigetui-launch-failure-desktop.png'

try {
    $runningProcesses = @(Get-Process -Name UniGetUI -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $currentSessionId })
    if ($runningProcesses.Count -eq 0) {
        throw "UniGetUI did not remain running in interactive session $currentSessionId."
    }

    $windowProcess = Wait-UniGetUIWindow -SessionId $currentSessionId
    Save-DesktopScreenshot -Path $desktopScreenshotPath
    Save-WindowScreenshot -WindowHandle $windowProcess.MainWindowHandle -Path $windowScreenshotPath
}
catch {
    try {
        Save-DesktopScreenshot -Path $failureScreenshotPath
        Write-Warning "Captured failure desktop screenshot at $failureScreenshotPath"
    }
    catch {
        Write-Warning "Could not capture failure desktop screenshot: $($_.Exception.Message)"
    }

    throw
}

$result = [pscustomobject]@{
    ReleaseTag = $release.tag_name
    InstallerAssetName = $installerAsset.name
    InstallerPath = $installerPath
    InstallerSha256 = $actualHash
    InstallDir = $installDir
    InstalledExe = $installedExe
    LauncherProcessId = $launchProcess.Id
    RunningProcessIds = @($runningProcesses | ForEach-Object { $_.Id })
    WindowProcessId = $windowProcess.Id
    MainWindowHandle = $windowProcess.MainWindowHandle.ToInt64()
    SessionId = $currentSessionId
    Screenshots = [pscustomobject]@{
        Desktop = $desktopScreenshotPath
        Window = $windowScreenshotPath
    }
}

$result | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $ArtifactsDir 'unigetui-launch.json') -Encoding utf8NoBOM
$result
