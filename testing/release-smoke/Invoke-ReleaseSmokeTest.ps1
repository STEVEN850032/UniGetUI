[CmdletBinding()]
param(
    [ValidateSet('rdp', 'rdp-client', 'remoting-server', 'remote-command', 'install-launch')]
    [string] $MaxStage = 'install-launch',

    [string] $ReleaseRepository = 'Devolutions/UniGetUI',

    [string] $ReleaseTag = '',

    [string] $InstallerAssetName = 'UniGetUI.Installer.x64.exe',

    [string] $ArtifactsDir = (Join-Path $env:GITHUB_WORKSPACE 'artifacts\release-smoke'),

    [switch] $CleanupOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$stageOrder = @('rdp', 'rdp-client', 'remoting-server', 'remote-command', 'install-launch')
$maxStageIndex = [array]::IndexOf($stageOrder, $MaxStage)
$scriptsRoot = $PSScriptRoot
$secretsDir = Join-Path $env:RUNNER_TEMP 'unigetui-release-smoke-secrets'
$rdpStatePath = Join-Path $ArtifactsDir 'rdp-state.json'
$rdpClientStatePath = Join-Path $ArtifactsDir 'rdp-client-state.json'
$endpointPath = Join-Path $ArtifactsDir 'pshost-endpoint.json'
$taskName = 'UniGetUIReleaseSmokePSHost'
$psHostPort = 45985

function Test-ShouldRunStage {
    param(
        [Parameter(Mandatory)]
        [string] $Stage
    )

    return ([array]::IndexOf($stageOrder, $Stage) -le $maxStageIndex)
}

function Stop-ProcessFromState {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $state = Get-Content -Path $Path -Raw | ConvertFrom-Json
    if ($null -eq $state.ProcessId) {
        return
    }

    $process = Get-Process -Id ([int] $state.ProcessId) -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        Stop-Process -Id $process.Id -Force
    }
}

function Invoke-Cleanup {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

    Stop-ProcessFromState -Path $rdpClientStatePath

    if (Test-Path $endpointPath) {
        $endpoint = Get-Content -Path $endpointPath -Raw | ConvertFrom-Json
        if ($endpoint.PSObject.Properties.Name -contains 'ProcessId') {
            $process = Get-Process -Id ([int] $endpoint.ProcessId) -ErrorAction SilentlyContinue
            if ($null -ne $process) {
                Stop-Process -Id $process.Id -Force
            }
        }
    }

    & (Join-Path $scriptsRoot 'Enable-LocalRdp.ps1') -Cleanup -StatePath $rdpStatePath
    Remove-Item -Path $secretsDir -Recurse -Force -ErrorAction SilentlyContinue
}

if ($CleanupOnly) {
    Invoke-Cleanup
    return
}

New-Item -Path $ArtifactsDir -ItemType Directory -Force | Out-Null
New-Item -Path $secretsDir -ItemType Directory -Force | Out-Null

$rdpInfo = $null

try {
    if (Test-ShouldRunStage -Stage 'rdp') {
        Write-Host '::group::Enable local RDP'
        $rdpInfoJson = & (Join-Path $scriptsRoot 'Enable-LocalRdp.ps1') -StatePath $rdpStatePath
        $rdpInfo = $rdpInfoJson | ConvertFrom-Json
        Write-Host '::endgroup::'
    }

    if (Test-ShouldRunStage -Stage 'remoting-server') {
        Write-Host '::group::Register interactive PSHostServer'
        & (Join-Path $scriptsRoot 'Start-InteractivePSHostServer.ps1') `
            -Register `
            -EndpointPath $endpointPath `
            -Port $psHostPort `
            -ArtifactsDir $ArtifactsDir `
            -TaskName $taskName | Write-Host
        Write-Host '::endgroup::'
    }

    if (Test-ShouldRunStage -Stage 'rdp-client') {
        if ($null -eq $rdpInfo) {
            throw 'RDP stage did not produce connection information.'
        }

        Write-Host '::group::Build and start IronRDP client'
        & (Join-Path $scriptsRoot 'Start-LocalRdpClient.ps1') `
            -UserName $rdpInfo.DomainUserName `
            -Password $rdpInfo.Password `
            -HostName $rdpInfo.HostName `
            -Port ([int] $rdpInfo.Port) `
            -ArtifactsDir $ArtifactsDir `
            -SecretsDir $secretsDir `
            -StatePath $rdpClientStatePath | Write-Host
        Write-Host '::endgroup::'
    }

    if (Test-ShouldRunStage -Stage 'remoting-server') {
        Write-Host '::group::Wait for interactive PSHostServer'
        & (Join-Path $scriptsRoot 'Start-InteractivePSHostServer.ps1') `
            -Wait `
            -EndpointPath $endpointPath `
            -TimeoutSeconds 180 | Write-Host
        Write-Host '::endgroup::'
    }

    if (Test-ShouldRunStage -Stage 'remote-command') {
        Write-Host '::group::Verify interactive remoting'
        & (Join-Path $scriptsRoot 'Invoke-InteractiveCommand.ps1') `
            -Mode Verify `
            -EndpointPath $endpointPath `
            -ArtifactsDir $ArtifactsDir | Write-Host
        Write-Host '::endgroup::'
    }

    if (Test-ShouldRunStage -Stage 'install-launch') {
        Write-Host '::group::Install and launch UniGetUI release'
        & (Join-Path $scriptsRoot 'Invoke-InteractiveCommand.ps1') `
            -Mode InstallLaunch `
            -EndpointPath $endpointPath `
            -ArtifactsDir $ArtifactsDir `
            -ReleaseRepository $ReleaseRepository `
            -ReleaseTag $ReleaseTag `
            -InstallerAssetName $InstallerAssetName `
            -GitHubToken $env:GITHUB_TOKEN | Write-Host
        Write-Host '::endgroup::'
    }
}
catch {
    Write-Host '::error::Release smoke test failed.'
    throw
}
