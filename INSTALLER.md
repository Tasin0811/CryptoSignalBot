# Installer Windows

Questi script servono per installare CryptoSignalBot su una macchina Windows sempre accesa.

## Requisiti sulla macchina

- Windows 10/11 o Windows Server.
- .NET 8 SDK se installi partendo dai sorgenti.
- SQL Server Express LocalDB oppure una connection string SQL Server valida.
- Credenziali Gmail/Telegram configurate come variabili ambiente utente.

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
- non scrive password nel repository.

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

## Orari custom

```powershell
.\scripts\Install-CryptoSignalBot.ps1 `
  -ReportDailyAt 08:30 `
  -CleanupDailyAt 03:00 `
  -InstallDashboardTask
```

## Test dopo installazione

```powershell
& "$env:ProgramData\CryptoSignalBot\app\Worker\CryptoSignalBot.Worker.exe" --smoke-test notifications
& "$env:ProgramData\CryptoSignalBot\app\Worker\CryptoSignalBot.Worker.exe" --report-watchlist --force-report
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
