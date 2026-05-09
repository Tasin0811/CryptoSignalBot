# Deploy gratuito di test su Render

Questa guida pubblica CryptoSignalBot come Web Service Docker su Render Free Tier.

La build espone la dashboard/API e mantiene il progetto in modalita' `DryRunOnly=true`. Non vengono eseguiti ordini reali e non sono presenti API di trading.

## Cosa viene avviato

- `CryptoSignalBot.Dashboard` come servizio web.
- `CryptoSignalBot.Worker` incluso nel container e richiamabile dalle azioni rapide della dashboard.
- Database SQLite file-based di test se non configuri un database esterno.
- Endpoint health: `GET /health`.

## Limiti importanti del Free Tier

Render Free e' adatto a prove temporanee, non produzione:

- il servizio puo' andare in sleep dopo circa 15 minuti senza traffico;
- il primo accesso dopo lo sleep puo' richiedere circa un minuto;
- il filesystem e' effimero: un database SQLite locale puo' essere perso a redeploy, restart o spin-down;
- le ore gratuite, bandwidth e build minutes sono limitate dal piano Render.

Per test di qualche giorno va bene. Per storico persistente serve un database esterno, per esempio Render Postgres o un SQL Server raggiungibile via connection string.

## File aggiunti

- `Dockerfile`: build multi-stage .NET 8.
- `.dockerignore`: esclude binari locali, log, database e file con segreti.
- `render.yaml`: blueprint Render opzionale.
- `CryptoSignalBot.Dashboard/appsettings.Production.json`: configurazione sicura per container.
- `CryptoSignalBot.Worker/appsettings.Production.json`: configurazione sicura per worker nel container.

## Variabili ambiente Render

Configura queste variabili nel servizio Render, se vuoi notifiche e database esterno:

| Variabile | Obbligatoria | Note |
| --- | --- | --- |
| `TELEGRAM_BOT_TOKEN` | No | Token del bot Telegram. Non inserirlo nel codice. |
| `TELEGRAM_CHAT_ID` | No | Chat id destinataria. |
| `SMTP_USER` | No | Account Gmail/SMTP. |
| `SMTP_PASSWORD` | No | App password Gmail/SMTP. |
| `SMTP_FROM` | No | Mittente email. |
| `SMTP_TO` | No | Destinatario email. |
| `DATABASE_CONNECTION_STRING` | No | Se vuota, usa SQLite di test in `/tmp/cryptosignalbot.db`. |
| `COINGECKO_API_KEY` | No | Opzionale. Se manca, il bot prova il contesto pubblico o continua senza CoinGecko. |

Render imposta la variabile `PORT`; l'app ascolta automaticamente su `0.0.0.0:$PORT`. Se `PORT` non esiste, usa `5055`.

## Deploy da interfaccia Render

1. Fai push del repository su GitHub.
2. Apri Render Dashboard.
3. Seleziona **New** -> **Web Service**.
4. Collega il repository `CryptoSignalBot`.
5. Come runtime/linguaggio scegli **Docker**.
6. Lascia `Dockerfile` come path: `./Dockerfile`.
7. Imposta piano **Free**.
8. Aggiungi le variabili ambiente necessarie.
9. Crea il servizio e attendi la build.

Quando Render mostra l'URL pubblico, verifica:

```text
https://NOME-SERVIZIO.onrender.com/health
```

La risposta attesa e':

```text
OK
```

Poi apri:

```text
https://NOME-SERVIZIO.onrender.com/
```

## Deploy tramite Blueprint

Puoi anche usare `render.yaml`:

1. Render Dashboard -> **New** -> **Blueprint**.
2. Seleziona il repository.
3. Render legge `render.yaml`.
4. Inserisci i valori marcati `sync: false` nella schermata Environment.

## Test locale Docker

Build:

```powershell
docker build -t cryptosignalbot-render .
```

Run locale:

```powershell
docker run --rm -p 5055:5055 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e PORT=5055 `
  -e DATABASE_CONNECTION_STRING="Data Source=/tmp/cryptosignalbot.db" `
  cryptosignalbot-render
```

Verifica health:

```powershell
Invoke-WebRequest http://localhost:5055/health
```

Dashboard:

```text
http://localhost:5055
```

## Sicurezza

- Non committare token, password, chat id o connection string private.
- `DryRunOnly` resta `true` di default.
- Il progetto non contiene Binance order API e non esegue trade reali.
- Render Docker rende disponibili le variabili ambiente anche in build: non usarle nel `Dockerfile` come `ARG` se contengono segreti.

## Note database

Default test:

```text
Data Source=/tmp/cryptosignalbot.db
```

Questo SQLite e' solo per prova. Se il servizio si riavvia o viene redeployato, i dati possono sparire.

Per usare SQL Server come sul PC, imposta:

```text
DATABASE_CONNECTION_STRING=Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;
```

Il codice sceglie SQLite quando la connection string inizia con `Data Source=` o se `Database:Provider` e' `Sqlite`; altrimenti resta su SQL Server.
