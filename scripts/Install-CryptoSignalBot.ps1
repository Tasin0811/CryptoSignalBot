<#
.SYNOPSIS
Installs CryptoSignalBot on a Windows machine.

.DESCRIPTION
Publishes the Worker and Dashboard, copies them to a stable install folder,
optionally stores non-repository user environment variables, and registers
Windows Scheduled Tasks for report, cleanup, and optional dashboard startup.

Run this from the repository root on the target machine.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ProjectRoot = '',

    [string]$InstallRoot = "$env:ProgramData\CryptoSignalBot",

    [string]$TaskPath = '\CryptoSignalBot\',

    [string]$ReportDailyAt = '08:00',

    [string]$CleanupDailyAt = '03:30',

    [string]$DashboardUrl = 'http://localhost:5055',

    [switch]$InstallDashboardTask,

    [switch]$ForceReport,

    [switch]$SendEmptyReport,

    [string]$SmtpUser = '',

    [string]$SmtpPassword = '',

    [string]$SmtpFrom = '',

    [string]$SmtpTo = '',

    [string]$TelegramBotToken = '',

    [string]$TelegramChatId = '',

    [string]$ConnectionString = ''
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

function Assert-Command {
    param([string]$Name, [string]$InstallHint)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name non trovato. $InstallHint"
    }
}

function Ensure-TaskFolder {
    param([string]$Path)

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

function New-DailyTrigger {
    param([string]$TimeOfDay)

    $parsed = [DateTime]::ParseExact(
        $TimeOfDay,
        'HH:mm',
        [Globalization.CultureInfo]::InvariantCulture)

    return New-ScheduledTaskTrigger -Daily -At $parsed
}

function Register-BotTask {
    param(
        [string]$Name,
        [string]$Executable,
        [string]$Arguments,
        [Microsoft.Management.Infrastructure.CimInstance]$Trigger,
        [string]$Description
    )

    $action = New-ScheduledTaskAction `
        -Execute $Executable `
        -Argument $Arguments `
        -WorkingDirectory (Split-Path -Parent $Executable)

    $settings = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew `
        -ExecutionTimeLimit (New-TimeSpan -Hours 2)

    $principal = New-ScheduledTaskPrincipal `
        -UserId $env:USERNAME `
        -LogonType Interactive `
        -RunLevel Limited

    if ($PSCmdlet.ShouldProcess("$TaskPath$Name", 'Register scheduled task')) {
        Register-ScheduledTask `
            -TaskName $Name `
            -TaskPath $TaskPath `
            -Action $action `
            -Trigger $Trigger `
            -Settings $settings `
            -Principal $principal `
            -Description $Description `
            -Force | Out-Null
    }
}

function Set-UserEnvironmentValue {
    param([string]$Name, [string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    if ($PSCmdlet.ShouldProcess("User environment variable $Name", 'Set')) {
        [Environment]::SetEnvironmentVariable($Name, $Value, 'User')
    }
}

$ProjectRoot = Resolve-ProjectRoot -Value $ProjectRoot
$InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
$appRoot = Join-Path $InstallRoot 'app'
$workerPublish = Join-Path $ProjectRoot 'artifacts\publish\worker'
$dashboardPublish = Join-Path $ProjectRoot 'artifacts\publish\dashboard'
$workerProject = Join-Path $ProjectRoot 'CryptoSignalBot.Worker\CryptoSignalBot.Worker.csproj'
$dashboardProject = Join-Path $ProjectRoot 'CryptoSignalBot.Dashboard\CryptoSignalBot.Dashboard.csproj'

Assert-Command -Name 'dotnet' -InstallHint 'Installa .NET 8 SDK o .NET 8 Runtime/Hosting Bundle.'

if (-not (Get-Command 'sqllocaldb' -ErrorAction SilentlyContinue)) {
    Write-Warning 'SQL Server LocalDB non trovato. Installa SQL Server Express LocalDB oppure passa -ConnectionString verso un SQL Server disponibile.'
}

if ($PSCmdlet.ShouldProcess($InstallRoot, 'Create install folders')) {
    New-Item -ItemType Directory -Force -Path $InstallRoot, $appRoot, (Join-Path $InstallRoot 'data'), (Join-Path $InstallRoot 'logs') | Out-Null
}

if ($PSCmdlet.ShouldProcess($ProjectRoot, 'Publish Worker and Dashboard')) {
    dotnet publish $workerProject -c Release -o $workerPublish --nologo
    dotnet publish $dashboardProject -c Release -o $dashboardPublish --nologo
}

if ($PSCmdlet.ShouldProcess($appRoot, 'Copy published files')) {
    $workerTarget = Join-Path $appRoot 'Worker'
    $dashboardTarget = Join-Path $appRoot 'Dashboard'
    New-Item -ItemType Directory -Force -Path $workerTarget, $dashboardTarget | Out-Null
    Copy-Item -Path (Join-Path $workerPublish '*') -Destination $workerTarget -Recurse -Force
    Copy-Item -Path (Join-Path $dashboardPublish '*') -Destination $dashboardTarget -Recurse -Force
}

Set-UserEnvironmentValue -Name 'Email__Username' -Value $SmtpUser
Set-UserEnvironmentValue -Name 'Email__Password' -Value $SmtpPassword
Set-UserEnvironmentValue -Name 'Email__From' -Value $SmtpFrom
Set-UserEnvironmentValue -Name 'Email__To' -Value $SmtpTo
Set-UserEnvironmentValue -Name 'Telegram__BotToken' -Value $TelegramBotToken
Set-UserEnvironmentValue -Name 'Telegram__ChatId' -Value $TelegramChatId
Set-UserEnvironmentValue -Name 'ConnectionStrings__CryptoSignalBot' -Value $ConnectionString
Set-UserEnvironmentValue -Name 'CRYPTO_SIGNAL_BOT_LOG_DIR' -Value (Join-Path $InstallRoot 'logs')

Ensure-TaskFolder -Path $TaskPath

$workerExe = Join-Path $appRoot 'Worker\CryptoSignalBot.Worker.exe'
$dashboardExe = Join-Path $appRoot 'Dashboard\CryptoSignalBot.Dashboard.exe'

$reportArgs = @('--report-watchlist')
if ($ForceReport) {
    $reportArgs += '--force-report'
}
if ($SendEmptyReport) {
    $reportArgs += '--send-empty-report'
}

Register-BotTask `
    -Name 'CryptoSignalBot Report Watchlist' `
    -Executable $workerExe `
    -Arguments ($reportArgs -join ' ') `
    -Trigger (New-DailyTrigger -TimeOfDay $ReportDailyAt) `
    -Description 'Runs CryptoSignalBot watchlist report and sends configured notifications.'

Register-BotTask `
    -Name 'CryptoSignalBot Cleanup DB' `
    -Executable $workerExe `
    -Arguments '--cleanup-db' `
    -Trigger (New-DailyTrigger -TimeOfDay $CleanupDailyAt) `
    -Description 'Runs CryptoSignalBot database retention cleanup.'

if ($InstallDashboardTask) {
    Register-BotTask `
        -Name 'CryptoSignalBot Dashboard' `
        -Executable $dashboardExe `
        -Arguments "--urls $DashboardUrl" `
        -Trigger (New-ScheduledTaskTrigger -AtLogOn) `
        -Description 'Starts the local CryptoSignalBot dashboard at user logon.'
}

Write-Host ''
Write-Host 'CryptoSignalBot installed.'
Write-Host "Install root: $InstallRoot"
Write-Host "Dashboard URL: $DashboardUrl"
Write-Host ''
Write-Host 'Next checks:'
Write-Host "  & `"$workerExe`" --smoke-test notifications"
Write-Host "  & `"$workerExe`" --report-watchlist --force-report"
if ($InstallDashboardTask) {
    Write-Host "  Start-ScheduledTask -TaskPath '$TaskPath' -TaskName 'CryptoSignalBot Dashboard'"
}
