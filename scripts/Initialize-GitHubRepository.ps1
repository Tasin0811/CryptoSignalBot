<#
.SYNOPSIS
Initializes this folder as a Git repository and pushes it to GitHub.

.DESCRIPTION
Run after creating an empty GitHub repository, for example:
https://github.com/Tasin0811/CryptoSignalBot
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$RemoteUrl,

    [string]$ProjectRoot = '',

    [string]$Branch = 'main',

    [string]$CommitMessage = 'Initial CryptoSignalBot commit'
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

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git non trovato. Installa Git for Windows: https://git-scm.com/download/win'
}

$ProjectRoot = Resolve-ProjectRoot -Value $ProjectRoot
Set-Location $ProjectRoot

if (-not (Test-Path '.git')) {
    if ($PSCmdlet.ShouldProcess($ProjectRoot, 'git init')) {
        git init
    }
}

if ($PSCmdlet.ShouldProcess($ProjectRoot, "set branch $Branch")) {
    git branch -M $Branch
}

$remoteNames = @(git remote)
if ($remoteNames -contains 'origin') {
    if ($PSCmdlet.ShouldProcess('origin', "set-url $RemoteUrl")) {
        git remote set-url origin $RemoteUrl
    }
}
else {
    if ($PSCmdlet.ShouldProcess('origin', "add $RemoteUrl")) {
        git remote add origin $RemoteUrl
    }
}

if ($PSCmdlet.ShouldProcess($ProjectRoot, 'git add/commit/push')) {
    git add .
    if ($LASTEXITCODE -ne 0) {
        throw 'git add failed.'
    }

    git status --short

    $hasChanges = git status --porcelain
    if (-not [string]::IsNullOrWhiteSpace($hasChanges)) {
        git commit -m $CommitMessage
        if ($LASTEXITCODE -ne 0) {
            throw 'git commit failed. Configure Git identity with: git config --global user.name "Your Name"; git config --global user.email "you@example.com"'
        }
    }

    git push -u origin $Branch
    if ($LASTEXITCODE -ne 0) {
        throw 'git push failed.'
    }
}

Write-Host "Repository pushed to $RemoteUrl"
