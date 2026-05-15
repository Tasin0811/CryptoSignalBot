# Database

Database target: SQL Server. In sviluppo usare:

```text
Server=(localdb)\MSSQLLocalDB;Database=CryptoSignalBot;Trusted_Connection=True;TrustServerCertificate=True;
```

## Tabelle V1

```sql
CREATE TABLE CryptoSymbols (
    Id INT IDENTITY PRIMARY KEY,
    Symbol NVARCHAR(30) NOT NULL UNIQUE,
    IsActive BIT NOT NULL DEFAULT 1,
    MinScoreToNotify DECIMAL(4,2) NOT NULL DEFAULT 7.50
);

CREATE TABLE MarketCandles (
    Id BIGINT IDENTITY PRIMARY KEY,
    Symbol NVARCHAR(30) NOT NULL,
    Timeframe NVARCHAR(10) NOT NULL,
    OpenTime DATETIME2 NOT NULL,
    OpenPrice DECIMAL(28,10) NOT NULL,
    HighPrice DECIMAL(28,10) NOT NULL,
    LowPrice DECIMAL(28,10) NOT NULL,
    ClosePrice DECIMAL(28,10) NOT NULL,
    Volume DECIMAL(28,10) NOT NULL,
    CONSTRAINT UQ_MarketCandles UNIQUE(Symbol, Timeframe, OpenTime)
);

CREATE TABLE Signals (
    Id BIGINT IDENTITY PRIMARY KEY,
    Symbol NVARCHAR(30) NOT NULL,
    Timeframe NVARCHAR(10) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Price DECIMAL(28,10) NOT NULL,
    Score DECIMAL(4,2) NOT NULL,
    SignalType NVARCHAR(20) NOT NULL,
    StopLoss DECIMAL(28,10) NULL,
    TakeProfit1 DECIMAL(28,10) NULL,
    TakeProfit2 DECIMAL(28,10) NULL,
    RiskReward DECIMAL(8,3) NULL,
    Summary NVARCHAR(MAX) NOT NULL
);

CREATE TABLE SignalRuleResults (
    Id BIGINT IDENTITY PRIMARY KEY,
    SignalId BIGINT NOT NULL FOREIGN KEY REFERENCES Signals(Id),
    RuleName NVARCHAR(100) NOT NULL,
    ScoreImpact DECIMAL(5,2) NOT NULL,
    Result NVARCHAR(30) NOT NULL,
    Details NVARCHAR(MAX) NULL
);

CREATE TABLE PaperTrades (
    Id BIGINT IDENTITY PRIMARY KEY,
    SignalId BIGINT NOT NULL UNIQUE,
    Symbol NVARCHAR(30) NOT NULL,
    Timeframe NVARCHAR(10) NOT NULL,
    EntryTime DATETIME2 NOT NULL,
    EntryPrice DECIMAL(28,10) NOT NULL,
    Units DECIMAL(28,10) NOT NULL,
    Invested DECIMAL(28,10) NOT NULL,
    RemainingUnits DECIMAL(28,10) NOT NULL,
    CashBefore DECIMAL(28,10) NOT NULL,
    CashAfter DECIMAL(28,10) NOT NULL,
    EntryFee DECIMAL(28,10) NOT NULL,
    ExitFee DECIMAL(28,10) NOT NULL,
    SlippageCost DECIMAL(28,10) NOT NULL,
    ExitTime DATETIME2 NULL,
    ExitPrice DECIMAL(28,10) NULL,
    CurrentPrice DECIMAL(28,10) NOT NULL,
    BreakEvenStop DECIMAL(28,10) NULL,
    Outcome NVARCHAR(30) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);
```

## Indici operativi

- `MarketCandles(Symbol, Timeframe, OpenTime)` unique: evita candele duplicate.
- `MarketCandles(OpenTime)`: accelera retention cleanup.
- `Signals(Symbol, Timeframe, SignalType, CreatedAt)`: accelera deduplica segnali.
- `Signals(CreatedAt)`: accelera retention cleanup.
- `PaperTrades(SignalId)` unique: evita duplicati nel wallet paper persistente.
- `PaperTrades(EntryTime)`: accelera report portfolio paper.

Il worker crea automaticamente il database se non esiste e verifica gli indici operativi su SQL Server LocalDB durante l'avvio della persistenza. In questo modo un database gia' presente resta aggiornabile senza cancellare lo storico.

Su SQLite/Hetzner, la tabella `PaperTrades` viene creata automaticamente se manca. I trade paper gia' salvati non vengono rimossi dal cleanup di candele/segnali.

## EF Core migrations

Il progetto Infrastructure contiene la factory design-time `CryptoSignalBotDbContextFactory` e la migration iniziale `InitialSqlServerLocalDb`.
La migration iniziale usa SQL idempotente: su un database LocalDB gia' creato con `EnsureCreated` non elimina dati e registra la baseline in `__EFMigrationsHistory`; su un database nuovo crea tabelle e indici.

Comandi consigliati dalla root della solution:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef database update --project .\CryptoSignalBot.Infrastructure\CryptoSignalBot.Infrastructure.csproj --startup-project .\CryptoSignalBot.Worker\CryptoSignalBot.Worker.csproj
```

Per generare una nuova migration dopo modifiche al modello:

```powershell
dotnet ef migrations add NomeMigration --project .\CryptoSignalBot.Infrastructure\CryptoSignalBot.Infrastructure.csproj --startup-project .\CryptoSignalBot.Worker\CryptoSignalBot.Worker.csproj --output-dir Persistence\Migrations
dotnet ef database update --project .\CryptoSignalBot.Infrastructure\CryptoSignalBot.Infrastructure.csproj --startup-project .\CryptoSignalBot.Worker\CryptoSignalBot.Worker.csproj
```

Per usare una connection string diversa da LocalDB:

```powershell
$env:ConnectionStrings__CryptoSignalBot = "Server=(localdb)\MSSQLLocalDB;Database=CryptoSignalBot;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet ef database update --project .\CryptoSignalBot.Infrastructure\CryptoSignalBot.Infrastructure.csproj --startup-project .\CryptoSignalBot.Worker\CryptoSignalBot.Worker.csproj
```
