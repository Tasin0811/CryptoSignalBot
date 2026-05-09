<#
.SYNOPSIS
Runs an end-to-end local validation for CryptoSignalBot.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = '',

    [switch]$SkipNotificationSmoke,

    [switch]$RunLiveReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ProjectRoot {
    param([string]$Value)

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        return (Resolve-Path $Value).Path
    }

    $scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    return (Resolve-Path (Join-Path $scriptRoot '..')).Path
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Script
    )

    Write-Host ""
    Write-Host "== $Name =="
    & $Script
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Name"
    }
}

$ProjectRoot = Resolve-ProjectRoot -Value $ProjectRoot
Set-Location $ProjectRoot

$env:DOTNET_CLI_HOME = if ($env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME } else { Join-Path $ProjectRoot '.dotnet_home' }
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:CRYPTO_SIGNAL_BOT_LOG_DIR = Join-Path $ProjectRoot 'logs'

Invoke-Step 'Restore' { dotnet restore .\CryptoSignalBot.sln }
Invoke-Step 'Build' { dotnet build .\CryptoSignalBot.sln --no-restore }
Invoke-Step 'Tests' { dotnet test .\CryptoSignalBot.sln --no-build }
Invoke-Step 'Cleanup DB command' { dotnet run --project .\CryptoSignalBot.Worker -- --cleanup-db }
Invoke-Step 'Backtest command' { dotnet run --project .\CryptoSignalBot.Worker -- --backtest-report }

if (-not $SkipNotificationSmoke) {
    Invoke-Step 'Notification smoke test' { dotnet run --project .\CryptoSignalBot.Worker -- --smoke-test notifications }
}

if ($RunLiveReport) {
    Invoke-Step 'Live watchlist report' { dotnet run --project .\CryptoSignalBot.Worker -- --report-watchlist --force-report }
}

Write-Host ""
Write-Host "CryptoSignalBot validation completed."
