# Deploy su Hetzner VPS

Questa guida installa CryptoSignalBot su una VPS Ubuntu con Docker Compose.

Il bot resta in `DryRunOnly=true`, usa SQLite persistente su volume Docker e invia notifiche Telegram se configuri token/chat id.

## Servizi Docker

- `dashboard`: dashboard web su porta `5055`.
- `scheduler`: esegue automaticamente:
  - report watchlist ogni 15 minuti;
  - paper trade report ogni 6 ore;
  - cleanup DB ogni 24 ore.

## Porte firewall

Aprire almeno:

- `TCP 22` per SSH.
- `TCP 5055` per dashboard.
- `ICMP` opzionale per ping.

## Primo setup server

Collegati al server:

```bash
ssh root@IP_SERVER
```

Aggiorna Ubuntu e installa Docker:

```bash
apt update && apt upgrade -y
apt install -y ca-certificates curl git
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" > /etc/apt/sources.list.d/docker.list
apt update
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Clona il progetto:

```bash
mkdir -p /opt/cryptosignalbot
git clone https://github.com/Tasin0811/CryptoSignalBot.git /opt/cryptosignalbot
cd /opt/cryptosignalbot
```

Crea configurazione locale:

```bash
cp .env.example .env
nano .env
```

Valori minimi:

```text
TELEGRAM_BOT_TOKEN=...
TELEGRAM_CHAT_ID=...
Bot__DryRunOnly=true
Bot__PaperPortfolioInitialBudget=500
Bot__AccountBalance=500
Bot__RiskPercent=0.01
```

Avvia:

```bash
docker compose up -d --build
```

Verifica:

```bash
docker compose ps
curl http://localhost:5055/health
```

Dashboard:

```text
http://IP_SERVER:5055
```

## Comandi utili

Log dashboard:

```bash
docker compose logs -f dashboard
```

Log scheduler:

```bash
docker compose logs -f scheduler
```

Forzare report ora:

```bash
docker compose exec dashboard dotnet /app/worker/CryptoSignalBot.Worker.dll --report-watchlist --force-report --send-empty-report
```

Test Telegram:

```bash
docker compose exec dashboard dotnet /app/worker/CryptoSignalBot.Worker.dll --smoke-test notifications
```

Aggiornare dopo un nuovo push:

```bash
cd /opt/cryptosignalbot
git pull
docker compose up -d --build
```

Spegnere:

```bash
docker compose down
```

Rimuovere anche database/log:

```bash
docker compose down -v
```

## Sicurezza

- Non incollare token in GitHub.
- `.env` resta solo sulla VPS.
- Lascia `Bot__DryRunOnly=true` durante tutto il test.
- Non sono presenti ordini reali o Binance order API.
