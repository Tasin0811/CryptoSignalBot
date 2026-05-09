# Gmail Setup

Per inviare email da Gmail con questa Worker app usa SMTP Gmail:

```text
Host: smtp.gmail.com
Porta: 587
Sicurezza: STARTTLS
Username: il tuo indirizzo Gmail completo
Password: app password Google a 16 caratteri
```

Google richiede 2-Step Verification per creare una app password. Non usare la password normale del tuo account Google.

## Variabili ambiente PowerShell

```powershell
$env:SMTP_USER="tuoaccount@gmail.com"
$env:SMTP_PASSWORD="xxxx xxxx xxxx xxxx"
$env:SMTP_FROM="tuoaccount@gmail.com"
$env:SMTP_TO="destinatario@gmail.com"
```

Poi lancia:

```powershell
cd D:\CODING\CryptoSignalBot
$env:DOTNET_CLI_HOME='D:\CODING\.dotnet'
dotnet run --project .\CryptoSignalBot.Worker\CryptoSignalBot.Worker.csproj -- --smoke-test notifications
```

Se vuoi testare anche Telegram nello stesso comando, imposta anche:

```powershell
$env:TELEGRAM_BOT_TOKEN="token-del-bot"
$env:TELEGRAM_CHAT_ID="chat-id"
```
