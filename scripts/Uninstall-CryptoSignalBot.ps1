<#
.SYNOPSIS
Uninstalls CryptoSignalBot Windows Scheduled Tasks and optionally files/env vars.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InstallRoot = "$env:ProgramData\CryptoSignalBot",

    [string]$TaskPath = '\CryptoSignalBot\',

    [switch]$RemoveFiles,

    [switch]$RemoveUserEnvironment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Remove-BotTask {
    param([string]$Name)

    $task = Get-ScheduledTask -TaskPath $TaskPath -TaskName $Name -ErrorAction SilentlyContinue
    if ($null -eq $task) {
        return
    }

    if ($PSCmdlet.ShouldProcess("$TaskPath$Name", 'Unregister scheduled task')) {
        Unregister-ScheduledTask -TaskPath $TaskPath -TaskName $Name -Confirm:$false
    }
}

function Remove-TaskFolderIfEmpty {
    param([string]$Path)

    $normalized = $Path.Trim()
    if ($normalized -eq '' -or $normalized -eq '\') {
        return
    }

    try {
        $service = New-Object -ComObject Schedule.Service
        $service.Connect()
        $root = $service.GetFolder('\')
        $folder = $service.GetFolder($normalized)
        if ($folder.GetTasks(0).Count -eq 0 -and $folder.GetFolders(0).Count -eq 0) {
            $root.DeleteFolder($normalized.Trim('\'), 0)
        }
    }
    catch {
        Write-Verbose "Task folder not removed: $($_.Exception.Message)"
    }
}

function Clear-UserEnvironmentValue {
    param([string]$Name)

    if ($PSCmdlet.ShouldProcess("User environment variable $Name", 'Remove')) {
        [Environment]::SetEnvironmentVariable($Name, $null, 'User')
    }
}

Remove-BotTask -Name 'CryptoSignalBot Report Watchlist'
Remove-BotTask -Name 'CryptoSignalBot Cleanup DB'
Remove-BotTask -Name 'CryptoSignalBot Dashboard'
Remove-TaskFolderIfEmpty -Path $TaskPath

if ($RemoveUserEnvironment) {
    Clear-UserEnvironmentValue -Name 'Email__Username'
    Clear-UserEnvironmentValue -Name 'Email__Password'
    Clear-UserEnvironmentValue -Name 'Email__From'
    Clear-UserEnvironmentValue -Name 'Email__To'
    Clear-UserEnvironmentValue -Name 'Telegram__BotToken'
    Clear-UserEnvironmentValue -Name 'Telegram__ChatId'
    Clear-UserEnvironmentValue -Name 'ConnectionStrings__CryptoSignalBot'
    Clear-UserEnvironmentValue -Name 'CRYPTO_SIGNAL_BOT_LOG_DIR'
}

if ($RemoveFiles) {
    $resolvedInstallRoot = [IO.Path]::GetFullPath($InstallRoot)
    if ($resolvedInstallRoot.Length -lt 10 -or -not $resolvedInstallRoot.Contains('CryptoSignalBot', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unsafe install root: $resolvedInstallRoot"
    }

    if (Test-Path $resolvedInstallRoot) {
        if ($PSCmdlet.ShouldProcess($resolvedInstallRoot, 'Remove install files')) {
            Remove-Item -LiteralPath $resolvedInstallRoot -Recurse -Force
        }
    }
}

Write-Host 'CryptoSignalBot uninstalled.'
if (-not $RemoveFiles) {
    Write-Host "Files kept at: $InstallRoot"
}
