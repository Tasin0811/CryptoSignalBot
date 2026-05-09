# Installer Windows

Questi script servono per installare CryptoSignalBot su una macchina Windows sempre accesa.

## Requisiti sulla macchina

- Windows 10/11 o Windows Server.
- .NET 8 SDK se installi partendo dai sorgenti.
- SQL Server Express LocalDB oppure una connection string SQL Server valida.
- Credenziali Gmail/Telegram configurate come variabili ambiente utente oppure passate all'installer.

## Installazione rapida

Da PowerShell, nella root del progetto:

```powershell
cd D:\CODING\CryptoSignalBot
.\scripts\Install-CryptoSignalBot.ps1 -InstallDashboardTask
```

Questo:

- pubblica Worker e Dashboard in `%ProgramData%\CryptoSignalBot\app`;
- crea task schedulate:
  - `CryptoSignalBot Report Watchlist`
  - `CryptoSignalBot Cleanup DB`
  - `CryptoSignalBot Dashboard` se usi `-InstallDashboardTask`;
- lascia i dati in `%ProgramData%\CryptoSignalBot\data`;
- scrive log in `%ProgramData%\CryptoSignalBot\logs`;
- non scrive password nel repository.

## Installazione automatica da file locale

Metodo consigliato per la tua macchina o per una macchina sempre accesa:

```powershell
Copy-Item .\install.example.json .\install.local.json
notepad .\install.local.json
.\scripts\Install-CryptoSignalBot.ps1 -ConfigPath .\install.local.json
```

`install.local.json` resta solo sul PC locale: e' escluso da GitHub tramite `.gitignore`.

Esempio:

```json
{
  "InstallDashboardTask": true,
  "RunNotificationSmoke": true,
  "ReportDailyAt": "08:00",
  "CleanupDailyAt": "03:30",
  "DashboardUrl": "http://localhost:5055",
  "SmtpUser": "tuamail@gmail.com",
  "SmtpPassword": "APP_PASSWORD_GOOGLE",
  "SmtpFrom": "tuamail@gmail.com",
  "SmtpTo": "destinatario@gmail.com",
  "TelegramBotToken": "TOKEN_TELEGRAM",
  "TelegramChatId": "CHAT_ID_TELEGRAM",
  "ConnectionString": ""
}
```

Puoi anche sovrascrivere un valore del file direttamente da comando:

```powershell
.\scripts\Install-CryptoSignalBot.ps1 -ConfigPath .\install.local.json -ReportDailyAt 09:00
```

## Installazione con Gmail

```powershell
.\scripts\Install-CryptoSignalBot.ps1 `
  -InstallDashboardTask `
  -SmtpUser "tuamail@gmail.com" `
  -SmtpPassword "APP_PASSWORD_GOOGLE" `
  -SmtpFrom "tuamail@gmail.com" `
  -SmtpTo "destinatario@gmail.com"
```

Le credenziali vengono salvate come variabili ambiente utente:

- `Email__Username`
- `Email__Password`
- `Email__From`
- `Email__To`

## Installazione con Gmail e Telegram

```powershell
.\scripts\Install-CryptoSignalBot.ps1 `
  -InstallDashboardTask `
  -RunNotificationSmoke `
  -SmtpUser "tuamail@gmail.com" `
  -SmtpPassword "APP_PASSWORD_GOOGLE" `
  -SmtpFrom "tuamail@gmail.com" `
  -SmtpTo "destinatario@gmail.com" `
  -TelegramBotToken "TOKEN_TELEGRAM" `
  -TelegramChatId "CHAT_ID_TELEGRAM"
```

L'installer salva i valori come variabili ambiente utente e lancia subito un test reale se usi `-RunNotificationSmoke`.

Variabili Telegram salvate:

- `Telegram__BotToken`
- `Telegram__ChatId`

## Orari custom

```powershell
.\scripts\Install-CryptoSignalBot.ps1 `
  -ReportDailyAt 08:30 `
  -CleanupDailyAt 03:00 `
  -InstallDashboardTask
```

## Test dopo installazione

Se non hai usato `-RunNotificationSmoke`, puoi testare manualmente:

```powershell
& "$env:ProgramData\CryptoSignalBot\app\Worker\CryptoSignalBot.Worker.exe" --smoke-test notifications
& "$env:ProgramData\CryptoSignalBot\app\Worker\CryptoSignalBot.Worker.exe" --report-watchlist --force-report
```

Log:

```powershell
Get-ChildItem "$env:ProgramData\CryptoSignalBot\logs"
```

Dashboard locale:

```text
http://localhost:5055
```

Se hai installato la dashboard task:

```powershell
Start-ScheduledTask -TaskPath '\CryptoSignalBot\' -TaskName 'CryptoSignalBot Dashboard'
```

## Disinstallazione

Rimuovere solo le task, tenendo file e dati:

```powershell
.\scripts\Uninstall-CryptoSignalBot.ps1
```

Rimuovere task e file installati:

```powershell
.\scripts\Uninstall-CryptoSignalBot.ps1 -RemoveFiles
```

Rimuovere anche variabili ambiente utente salvate:

```powershell
.\scripts\Uninstall-CryptoSignalBot.ps1 -RemoveFiles -RemoveUserEnvironment
```
