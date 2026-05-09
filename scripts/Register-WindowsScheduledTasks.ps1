<#
.SYNOPSIS
Registers Windows Scheduled Tasks for CryptoSignalBot maintenance jobs.

.DESCRIPTION
Creates or updates two tasks for the current Windows user:
- CryptoSignalBot Report Watchlist
- CryptoSignalBot Cleanup DB

The tasks run the Worker with command-line switches only. Secrets are not
stored in the scheduled task definition; keep tokens and passwords in
user-secrets, user/machine environment variables, or another secret manager.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ProjectRoot = '',

    [string]$TaskPath = '\CryptoSignalBot\',

    [string]$DotnetPath = 'dotnet',

    [string]$ReportTaskName = 'CryptoSignalBot Report Watchlist',

    [string]$CleanupTaskName = 'CryptoSignalBot Cleanup DB',

    [string]$ReportDailyAt = '08:00',

    [string]$CleanupDailyAt = '03:30',

    [switch]$ForceReport,

    [switch]$SendEmptyReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-ToDailyTrigger {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TimeOfDay
    )

    $parsed = [DateTime]::ParseExact(
        $TimeOfDay,
        'HH:mm',
        [Globalization.CultureInfo]::InvariantCulture)

    return New-ScheduledTaskTrigger -Daily -At $parsed
}

function Resolve-ExecutablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable
    )

    if ([IO.Path]::IsPathRooted($Executable)) {
        return $Executable
    }

    $command = Get-Command $Executable -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $Executable
}

function Ensure-TaskFolder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalized = $Path.Trim()
    if ($normalized -eq '' -or $normalized -eq '\') {
        return
    }

    $service = New-Object -ComObject Schedule.Service
    $service.Connect()
    $folder = $service.GetFolder('\')
    $parts = $normalized.Trim('\').Split('\', [StringSplitOptions]::RemoveEmptyEntries)

    foreach ($part in $parts) {
        $childPath = (($folder.Path.TrimEnd('\') + '\' + $part).Replace('/', '\'))
        try {
            $folder = $service.GetFolder($childPath)
        }
        catch {
            $folder = $folder.CreateFolder($part)
        }
    }
}

function Register-CryptoSignalBotTask {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TaskName,

        [Parameter(Mandatory = $true)]
        [string]$WorkerArguments,

        [Parameter(Mandatory = $true)]
        [Microsoft.Management.Infrastructure.CimInstance]$Trigger,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $projectPath = Join-Path $ProjectRoot 'CryptoSignalBot.Worker'
    $arguments = "run --project `"$projectPath`" -- $WorkerArguments"
    $action = New-ScheduledTaskAction -Execute $DotnetPath -Argument $arguments -WorkingDirectory $ProjectRoot
    $settings = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew `
        -ExecutionTimeLimit (New-TimeSpan -Hours 2)

    $principal = New-ScheduledTaskPrincipal `
        -UserId $env:USERNAME `
        -LogonType Interactive `
        -RunLevel Limited

    if ($PSCmdlet.ShouldProcess("$TaskPath$TaskName", 'Register scheduled task')) {
        Register-ScheduledTask `
            -TaskName $TaskName `
            -TaskPath $TaskPath `
            -Action $action `
            -Trigger $Trigger `
            -Settings $settings `
            -Principal $principal `
            -Description $Description `
            -Force | Out-Null
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $ProjectRoot = Join-Path $scriptRoot '..'
}

$resolvedProjectRoot = Resolve-Path $ProjectRoot
$ProjectRoot = $resolvedProjectRoot.Path
$DotnetPath = Resolve-ExecutablePath -Executable $DotnetPath

if ($PSCmdlet.ShouldProcess($TaskPath, 'Ensure scheduled task folder exists')) {
    Ensure-TaskFolder -Path $TaskPath
}

$reportArgs = @('--report-watchlist')
if ($ForceReport) {
    $reportArgs += '--force-report'
}
if ($SendEmptyReport) {
    $reportArgs += '--send-empty-report'
}

Register-CryptoSignalBotTask `
    -TaskName $ReportTaskName `
    -WorkerArguments ($reportArgs -join ' ') `
    -Trigger (Convert-ToDailyTrigger -TimeOfDay $ReportDailyAt) `
    -Description 'Runs CryptoSignalBot watchlist report and sends the configured notification summary.'

Register-CryptoSignalBotTask `
    -TaskName $CleanupTaskName `
    -WorkerArguments '--cleanup-db' `
    -Trigger (Convert-ToDailyTrigger -TimeOfDay $CleanupDailyAt) `
    -Description 'Runs CryptoSignalBot database retention cleanup.'

Write-Host "Registered scheduled tasks under $TaskPath"
Write-Host "- $ReportTaskName daily at $ReportDailyAt"
Write-Host "- $CleanupTaskName daily at $CleanupDailyAt"
