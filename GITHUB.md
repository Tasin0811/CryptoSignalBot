# GitHub Setup

Questa guida serve per caricare CryptoSignalBot su GitHub e scaricarlo da qualsiasi macchina.

## 1. Crea un repository vuoto

Consigliato:

- Owner: `Tasin0811`
- Name: `CryptoSignalBot`
- Visibility: `Private`
- Non aggiungere README/gitignore/license dal sito, perche' il progetto li ha gia'.

URL atteso:

```text
https://github.com/Tasin0811/CryptoSignalBot
```

## 2. Installa Git sulla macchina

Se `git` non e' disponibile:

```powershell
winget install --id Git.Git -e
```

Poi chiudi e riapri PowerShell.

## 3. Pubblica il progetto

```powershell
cd D:\CODING\CryptoSignalBot
.\scripts\Initialize-GitHubRepository.ps1 -RemoteUrl "https://github.com/Tasin0811/CryptoSignalBot.git"
```

Se GitHub chiede login, usa il browser o un token GitHub.

## 4. Scaricarlo su un'altra macchina

```powershell
cd D:\CODING
git clone https://github.com/Tasin0811/CryptoSignalBot.git
cd CryptoSignalBot
dotnet restore
dotnet build
```

Poi configura le credenziali Gmail sulla nuova macchina con l'installer:

```powershell
.\scripts\Install-CryptoSignalBot.ps1 `
  -InstallDashboardTask `
  -SmtpUser "tuamail@gmail.com" `
  -SmtpPassword "APP_PASSWORD_GOOGLE" `
  -SmtpFrom "tuamail@gmail.com" `
  -SmtpTo "destinatario@gmail.com"
```

## Sicurezza

Non caricare mai:

- password Gmail;
- token Telegram;
- file `.env`;
- `secrets.json`;
- database locali;
- cartelle `bin`, `obj`, `artifacts`, `publish`.

La `.gitignore` del progetto li esclude.
