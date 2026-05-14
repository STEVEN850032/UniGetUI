[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $UserName,

    [Parameter(Mandatory)]
    [string] $Password,

    [string] $HostName = '127.0.0.1',

    [int] $Port = 3389,

    [string] $ArtifactsDir = (Join-Path $env:GITHUB_WORKSPACE 'artifacts\release-smoke'),

    [string] $SecretsDir = (Join-Path $env:RUNNER_TEMP 'unigetui-release-smoke-secrets'),

    [string] $StatePath = (Join-Path $ArtifactsDir 'rdp-client-state.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory)]
        [string] $LogPath,

        [Parameter(Mandatory)]
        [scriptblock] $ScriptBlock
    )

    New-Item -Path (Split-Path -Path $LogPath -Parent) -ItemType Directory -Force | Out-Null
    & $ScriptBlock *>&1 | Tee-Object -FilePath $LogPath
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE. See $LogPath."
    }
}

function Ensure-Cargo {
    if (Get-Command cargo -ErrorAction SilentlyContinue) {
        return
    }

    $rustupPath = Join-Path $env:RUNNER_TEMP 'rustup-init.exe'
    Invoke-WebRequest -Uri 'https://win.rustup.rs/x86_64' -OutFile $rustupPath
    & $rustupPath -y --default-toolchain stable --profile minimal
    $env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"

    if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
        throw 'cargo was not found after installing Rust with rustup.'
    }
}

New-Item -Path $ArtifactsDir -ItemType Directory -Force | Out-Null
New-Item -Path $SecretsDir -ItemType Directory -Force | Out-Null

Ensure-Cargo

$ironRdpRoot = Join-Path $env:RUNNER_TEMP 'IronRDP'
$buildLogPath = Join-Path $ArtifactsDir 'ironrdp-build.log'

if (-not (Test-Path $ironRdpRoot)) {
    Invoke-LoggedCommand -LogPath (Join-Path $ArtifactsDir 'ironrdp-clone.log') -ScriptBlock {
        git clone --depth 1 https://github.com/Devolutions/IronRDP.git $ironRdpRoot
    }
}

Push-Location $ironRdpRoot
try {
    Invoke-LoggedCommand -LogPath $buildLogPath -ScriptBlock {
        cargo build --release -p ironrdp-client
    }
}
finally {
    Pop-Location
}

$clientPath = Join-Path $ironRdpRoot 'target\release\ironrdp-client.exe'
if (-not (Test-Path $clientPath)) {
    throw "IronRDP client was not produced at $clientPath."
}

$rdpFile = Join-Path $SecretsDir 'localhost.rdp'
@(
    "full address:s:$HostName"
    "server port:i:$Port"
    "username:s:$UserName"
    "ClearTextPassword:s:$Password"
    'enablecredsspsupport:i:0'
    'desktopwidth:i:1280'
    'desktopheight:i:720'
    'audiomode:i:2'
    'redirectclipboard:i:0'
) | Set-Content -Path $rdpFile -Encoding ascii

$stdoutPath = Join-Path $ArtifactsDir 'ironrdp-client.stdout.log'
$stderrPath = Join-Path $ArtifactsDir 'ironrdp-client.stderr.log'
$clientLogPath = Join-Path $ArtifactsDir 'ironrdp-client.log'
$arguments = @(
    '--rdp-file', $rdpFile,
    '--autologon',
    '--no-credssp',
    '--prevent-session-lock', '1',
    '--log-file', $clientLogPath
)

$process = Start-Process -FilePath $clientPath -ArgumentList $arguments -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
Start-Sleep -Seconds 20

if ($process.HasExited) {
    throw "IronRDP client exited early with code $($process.ExitCode). See $stdoutPath and $stderrPath."
}

[pscustomobject]@{
    ProcessId = $process.Id
    ClientPath = $clientPath
    ClientLogPath = $clientLogPath
    StandardOutputPath = $stdoutPath
    StandardErrorPath = $stderrPath
} | ConvertTo-Json -Depth 4 | Set-Content -Path $StatePath -Encoding utf8NoBOM

[pscustomobject]@{
    ProcessId = $process.Id
    ClientPath = $clientPath
    StatePath = $StatePath
} | ConvertTo-Json -Compress
