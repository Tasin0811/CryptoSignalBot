<#
.SYNOPSIS
Sends a direct Telegram test message using local environment variables.

.DESCRIPTION
Reads Telegram__BotToken and Telegram__ChatId from the current process first,
then from user environment variables. The token is never printed.
#>
[CmdletBinding()]
param(
    [string]$Message = '',

    [string]$BotToken = '',

    [string]$ChatId = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-SecretValue {
    param([string]$ExplicitValue, [string]$EnvironmentName)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitValue)) {
        return $ExplicitValue
    }

    $processValue = [Environment]::GetEnvironmentVariable($EnvironmentName, 'Process')
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue
    }

    return [Environment]::GetEnvironmentVariable($EnvironmentName, 'User')
}

$resolvedToken = Resolve-SecretValue -ExplicitValue $BotToken -EnvironmentName 'Telegram__BotToken'
$resolvedChatId = Resolve-SecretValue -ExplicitValue $ChatId -EnvironmentName 'Telegram__ChatId'

if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    throw 'Telegram__BotToken non configurato.'
}

if ([string]::IsNullOrWhiteSpace($resolvedChatId)) {
    throw 'Telegram__ChatId non configurato.'
}

if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = "CryptoSignalBot Telegram test - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
}

$uri = "https://api.telegram.org/bot$resolvedToken/sendMessage"
$body = @{
    chat_id = $resolvedChatId
    text = $Message
}

$response = Invoke-RestMethod -Method Post -Uri $uri -Body $body
if (-not $response.ok) {
    throw "Telegram API returned ok=false."
}

[pscustomobject]@{
    Ok = $response.ok
    MessageId = $response.result.message_id
    ChatId = $response.result.chat.id
    ChatType = $response.result.chat.type
} | Format-Table -AutoSize
