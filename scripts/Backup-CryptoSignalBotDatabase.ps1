<#
.SYNOPSIS
Creates a SQL Server backup of the CryptoSignalBot database.
#>
[CmdletBinding()]
param(
    [string]$BackupRoot = "$env:ProgramData\CryptoSignalBot\backups",

    [string]$SqlInstance = '(localdb)\MSSQLLocalDB',

    [string]$Database = 'CryptoSignalBot'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'sqlcmd non trovato. Installa SQL Server command line tools oppure fai il backup dal tool SQL Server disponibile.'
}

$BackupRoot = [IO.Path]::GetFullPath($BackupRoot)
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupFile = Join-Path $BackupRoot "$Database-$timestamp.bak"
$query = "BACKUP DATABASE [$Database] TO DISK = N'$backupFile' WITH INIT, COMPRESSION;"

sqlcmd -S $SqlInstance -E -C -Q $query
if ($LASTEXITCODE -ne 0) {
    throw 'Database backup failed.'
}

Write-Host "Backup created: $backupFile"
