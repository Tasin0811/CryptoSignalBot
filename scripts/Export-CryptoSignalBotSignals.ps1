<#
.SYNOPSIS
Exports recent signals from the local dashboard API to CSV.
#>
[CmdletBinding()]
param(
    [string]$DashboardUrl = 'http://localhost:5055',

    [int]$Days = 30,

    [int]$Take = 1000,

    [string]$OutputPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $exportRoot = Join-Path (Resolve-Path '.').Path 'exports'
    New-Item -ItemType Directory -Force -Path $exportRoot | Out-Null
    $OutputPath = Join-Path $exportRoot ("signals-{0}.csv" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

$uri = '{0}/api/export/signals.csv?days={1}&take={2}' -f $DashboardUrl.TrimEnd('/'), $Days, $Take
Invoke-WebRequest -Uri $uri -OutFile $OutputPath -UseBasicParsing

Write-Host "Signals exported: $OutputPath"
