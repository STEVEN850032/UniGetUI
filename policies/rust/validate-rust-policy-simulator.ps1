param(
    [string]$PolicyRoot = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'

$workspaceManifest = Join-Path $PSScriptRoot 'Cargo.toml'
$resolvedPolicyRoot = (Resolve-Path $PolicyRoot).Path

Write-Host "Running consolidated Rust validation..."
cargo run --manifest-path $workspaceManifest -p unigetui_policy_simulator_validate -- --policy-root $resolvedPolicyRoot
