# Crypto Signal Bot

Bot di analisi crypto per generare segnali `AVOID`, `WAIT`, `WATCH`, `BUY WATCH` e `HIGH QUALITY SETUP`.

La V1 e' solo un assistente decisionale: invia alert Telegram/email e salva lo storico, ma non esegue ordini reali.

## Stack

- C# / .NET 8 Worker Service
- SQL Server LocalDB per sviluppo, SQL Server Express/SQL Server per ambienti stabili
- Binance Spot REST API per market data pubblici
- CoinGecko API per contesto crypto globale
- Telegram.Bot per notifiche Telegram
- MailKit per email
- Skender.Stock.Indicators per indicatori tecnici
- Serilog per logging

## Progetti

- `CryptoSignalBot.Domain`: modelli e tipi core.
- `CryptoSignalBot.Application`: regole, indicatori, scoring e risk engine.
- `CryptoSignalBot.Infrastructure`: client API, database, notifiche e integrazioni.
- `CryptoSignalBot.Worker`: processo schedulato e composizione applicativa.
- `CryptoSignalBot.Dashboard`: dashboard locale per stato, segnali recenti, paper summary e modifica sicura delle impostazioni bot.

## Setup locale

```powershell
cd D:\CODING\CryptoSignalBot
$env:DOTNET_CLI_HOME='D:\CODING\.dotnet'
dotnet restore
dotnet build
```

## Installer Windows

Per installare il bot su una macchina Windows sempre accesa usa:

```powershell
.\scripts\Install-CryptoSignalBot.ps1 -InstallDashboardTask
```

Per rimuoverlo:

```powershell
.\scripts\Uninstall-CryptoSignalBot.ps1
```

Guida completa: `INSTALLER.md`.

## GitHub

Per pubblicare il progetto su GitHub e scaricarlo da altre macchine:

```powershell
.\scripts\Initialize-GitHubRepository.ps1 -RemoteUrl "https://github.com/Tasin0811/CryptoSignalBot.git"
```

Guida completa: `GITHUB.md`.

## Comandi utili

Dashboard grafica:

```powershell
dotnet run --project .\CryptoSignalBot.Dashboard -- --urls http://localhost:5055
```

Aprire `http://localhost:5055`. Dalla dashboard puoi modificare impostazioni non segrete e lanciare report, paper report, backtest e cleanup DB.

Smoke test notifiche Gmail/Telegram:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --smoke-test notifications
```

Analisi singola con dati Binance reali:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --analyze-once BTCUSDT 1h
```

L'analisi singola salva candele e segnale su LocalDB. Invia una notifica solo se lo score e' maggiore o uguale a `Bot:MinScoreToNotify`.

Analisi di tutta la watchlist configurata:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --analyze-watchlist
```

Report unico della watchlist:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --report-watchlist
```

Forzare il report ignorando la deduplica temporale:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --report-watchlist --force-report
```

Inviare anche report vuoti:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --report-watchlist --send-empty-report
```

Paper trading report sui segnali salvati:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --paper-trade-report
```

Dashboard locale:

```powershell
dotnet run --project .\CryptoSignalBot.Dashboard
```

Aprire l'URL indicato da ASP.NET Core, ad esempio `http://localhost:5000` o `https://localhost:5001`.
La dashboard legge la configurazione della Worker e permette di modificare solo la sezione `Bot` in `CryptoSignalBot.Worker\appsettings.json`.
Non espone token Telegram, credenziali SMTP, API key o connection string.

Endpoint dashboard utili:

- `GET /api/status`: stato API/database e impostazioni bot non segrete.
- `GET /api/signals/recent?days=7&take=30`: ultimi segnali salvati.
- `GET /api/paper/summary?maxSignals=100&maxFutureCandles=24`: riepilogo paper trading.
- `GET /api/tasks/status`: stato Windows Scheduled Tasks.
- `GET /api/export/signals.csv?days=30&take=1000`: export CSV segnali.
- `POST /api/settings/bot`: salva solo impostazioni bot sanificate.
- `POST /api/commands/report-watchlist`: invia report watchlist ora.
- `POST /api/commands/paper-trade-report`: genera paper report ora.
- `POST /api/commands/backtest-report`: esegue backtest ora.
- `POST /api/commands/cleanup-db`: pulisce il database ora.

Test automatico locale:

```powershell
.\scripts\Test-CryptoSignalBot.ps1 -SkipNotificationSmoke
```

Export segnali CSV dalla dashboard avviata:

```powershell
.\scripts\Export-CryptoSignalBotSignals.ps1
```

Backup database LocalDB, se `sqlcmd` e' installato:

```powershell
.\scripts\Backup-CryptoSignalBotDatabase.ps1
```

Pulizia DB secondo retention configurata:

```powershell
dotnet run --project .\CryptoSignalBot.Worker -- --cleanup-db
```

## Scheduler Windows

Il percorso piu' semplice su Windows e' registrare due Scheduled Tasks che eseguono la Worker con gli switch gia' supportati:

- `--report-watchlist` per inviare il report watchlist.
- `--cleanup-db` per applicare la retention del database.

Da PowerShell:

```powershell
cd D:\CODING\CryptoSignalBot
.\scripts\Register-WindowsScheduledTasks.ps1
```

Orari personalizzati:

```powershell
.\scripts\Register-WindowsScheduledTasks.ps1 -ReportDailyAt 08:30 -CleanupDailyAt 03:00
```

Opzioni utili per il report:

```powershell
.\scripts\Register-WindowsScheduledTasks.ps1 -ForceReport
.\scripts\Register-WindowsScheduledTasks.ps1 -SendEmptyReport
```

Lo script crea o aggiorna:

- `\CryptoSignalBot\CryptoSignalBot Report Watchlist`
- `\CryptoSignalBot\CryptoSignalBot Cleanup DB`

Le task non salvano segreti negli argomenti o nella definizione. Configurare token Telegram, SMTP e connection string con user-secrets, variabili ambiente utente/macchina o un secret manager. Per verificare una task appena creata:

```powershell
Start-ScheduledTask -TaskPath '\CryptoSignalBot\' -TaskName 'CryptoSignalBot Report Watchlist'
Get-ScheduledTaskInfo -TaskPath '\CryptoSignalBot\' -TaskName 'CryptoSignalBot Report Watchlist'
```

Database di sviluppo:

```powershell
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "SELECT @@VERSION;"
```

## Sicurezza

Non salvare token Telegram, credenziali SMTP o API key nel repository. Usare variabili ambiente, user-secrets o secret manager.
