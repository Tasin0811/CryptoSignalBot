using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoSignalBot.Application;
using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Dashboard;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Infrastructure;
using CryptoSignalBot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var startedAt = DateTimeOffset.UtcNow;
ConfigureRenderPort(builder);

var projectRoot = ResolveProjectRoot(builder.Environment.ContentRootPath);
var workerSettingsPath = ResolveWorkerSettingsPath(builder.Environment.ContentRootPath);
var workerProductionSettingsPath = ResolveEnvironmentSettingsPath(
    workerSettingsPath,
    builder.Environment.EnvironmentName);
builder.Configuration
    .AddJsonFile(workerSettingsPath, optional: true, reloadOnChange: true)
    .AddJsonFile(workerProductionSettingsPath, optional: true, reloadOnChange: true)
    .AddCryptoSignalBotEnvironmentVariables();
builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("Bot"));
builder.Services.Configure<BinanceSettings>(builder.Configuration.GetSection("Binance"));
builder.Services.Configure<CoinGeckoSettings>(builder.Configuration.GetSection("CoinGecko"));
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Text("OK", "text/plain"));
app.MapGet("/", () => Results.Content(DashboardPage.Html, "text/html"));

app.MapGet("/api/status", async (
    IPersistenceService persistenceService,
    IOptions<BotSettings> botSettings,
    CancellationToken cancellationToken) =>
{
    var utcNow = DateTimeOffset.UtcNow;
    var since = utcNow.AddDays(-7);

    try
    {
        var recentSignals = await persistenceService.GetSignalsSinceAsync(since, cancellationToken);

        return (IResult)Results.Ok(new
        {
            status = "ok",
            utcNow,
            startedAt,
            database = new
            {
                status = "ok",
                signalCountLast7Days = recentSignals.Count,
                latestSignalAt = recentSignals
                    .OrderByDescending(signal => signal.CreatedAt)
                    .Select(signal => (DateTimeOffset?)signal.CreatedAt)
                    .FirstOrDefault()
            },
            settings = BotSettingsDto.From(botSettings.Value)
        });
    }
    catch
    {
        return (IResult)Results.Json(new
        {
            status = "degraded",
            utcNow,
            startedAt,
            database = new
            {
                status = "error",
                message = "Database unavailable."
            },
            settings = BotSettingsDto.From(botSettings.Value)
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/signals/recent", async (
    IPersistenceService persistenceService,
    int? days,
    int? take,
    CancellationToken cancellationToken) =>
{
    var lookbackDays = Math.Clamp(days.GetValueOrDefault(7), 1, 90);
    var maxSignals = Math.Clamp(take.GetValueOrDefault(30), 1, 200);
    var recentSignals = await persistenceService.GetSignalsSinceAsync(
        DateTimeOffset.UtcNow.AddDays(-lookbackDays),
        cancellationToken);

    return Results.Ok(new
    {
        days = lookbackDays,
        take = maxSignals,
        signals = recentSignals
            .OrderByDescending(signal => signal.CreatedAt)
            .Take(maxSignals)
            .Select(signal => new
            {
                signal.Symbol,
                signal.Timeframe,
                signal.CreatedAt,
                signal.Price,
                signal.Score,
                SignalType = signal.SignalType.ToString(),
                RiskLevel = signal.RiskLevel.ToString(),
                signal.StopLoss,
                signal.TakeProfit1,
                signal.RiskReward,
                signal.Summary
            })
    });
});

app.MapGet("/api/paper/summary", async (
    IPersistenceService persistenceService,
    int? maxSignals,
    int? maxFutureCandles,
    CancellationToken cancellationToken) =>
{
    var signalLimit = Math.Clamp(maxSignals.GetValueOrDefault(100), 1, 500);
    var candleLimit = Math.Clamp(maxFutureCandles.GetValueOrDefault(24), 1, 500);
    var report = await persistenceService.BuildPaperTradeReportAsync(signalLimit, candleLimit, cancellationToken);

    return Results.Ok(new
    {
        report.CreatedAt,
        maxSignals = signalLimit,
        maxFutureCandles = candleLimit,
        total = report.Results.Count,
        report.ClosedCount,
        report.Wins,
        report.Losses,
        report.OpenCount,
        report.ExpiredCount,
        report.InvalidCount,
        report.WinRate,
        recentClosedTrades = report.RecentClosedTrades(10).Select(ToPaperTradeDto),
        outcomes = Enum.GetValues<PaperTradeOutcome>()
            .ToDictionary(
                outcome => outcome.ToString(),
                outcome => report.Results.Count(result => result.Outcome == outcome))
    });
});

app.MapGet("/api/paper/portfolio", async (
    IPersistenceService persistenceService,
    IOptions<BotSettings> botSettings,
    decimal? initialBudget,
    int? maxSignals,
    int? maxFutureCandles,
    CancellationToken cancellationToken) =>
{
    var budget = Math.Clamp(
        initialBudget.GetValueOrDefault(botSettings.Value.PaperPortfolioInitialBudget),
        10m,
        1_000_000m);
    var signalLimit = Math.Clamp(maxSignals.GetValueOrDefault(100), 1, 500);
    var candleLimit = Math.Clamp(maxFutureCandles.GetValueOrDefault(24), 1, 500);
    var report = await persistenceService.BuildPaperPortfolioReportAsync(
        budget,
        signalLimit,
        candleLimit,
        cancellationToken);

    return Results.Ok(new
    {
        report.CreatedAt,
        report.InitialBudget,
        report.Cash,
        report.OpenPositionValue,
        report.Equity,
        report.ProfitLoss,
        report.ProfitLossPercent,
        report.FirstTradeAt,
        report.LastTradeAt,
        report.TotalInvested,
        report.RealizedProfitLoss,
        report.UnrealizedProfitLoss,
        report.TotalFees,
        report.CapitalAtWorkPercent,
        report.AverageInvested,
        report.BestTradeProfitLoss,
        report.WorstTradeProfitLoss,
        report.AverageWin,
        report.AverageLoss,
        report.ProfitFactor,
        report.Expectancy,
        report.ExpectancyPercent,
        report.MaxDrawdown,
        report.MaxDrawdownPercent,
        report.ClosedCount,
        report.OpenCount,
        report.Wins,
        report.Losses,
        report.WinRate,
        trades = report.Trades
            .OrderByDescending(trade => trade.EntryTime)
            .Take(20)
            .Select(ToPaperPortfolioTradeDto),
        equityCurve = report.EquityCurve.Select(point => new
        {
            point.Time,
            point.Equity,
            point.ProfitLoss,
            point.Label
        })
    });
});

app.MapGet("/api/tasks/status", () => Results.Ok(new
{
    tasks = TaskStatusReader.Read()
}));

app.MapGet("/api/export/signals.csv", async (
    IPersistenceService persistenceService,
    int? days,
    int? take,
    CancellationToken cancellationToken) =>
{
    var lookbackDays = Math.Clamp(days.GetValueOrDefault(30), 1, 3650);
    var maxSignals = Math.Clamp(take.GetValueOrDefault(1000), 1, 10000);
    var recentSignals = await persistenceService.GetSignalsSinceAsync(
        DateTimeOffset.UtcNow.AddDays(-lookbackDays),
        cancellationToken);

    var csv = CsvExporter.FormatSignals(
        recentSignals
            .OrderByDescending(signal => signal.CreatedAt)
            .Take(maxSignals));

    return Results.Text(csv, "text/csv");
});

app.MapPost("/api/settings/bot", async (BotSettingsDto input, CancellationToken cancellationToken) =>
{
    var sanitized = input.Sanitize();
    await UpdateBotSettingsAsync(workerSettingsPath, sanitized, cancellationToken);
    return Results.Ok(new { saved = true, settings = sanitized });
});

app.MapPost("/api/commands/{commandName}", async (string commandName, CancellationToken cancellationToken) =>
{
    if (!DashboardCommand.TryCreate(commandName, projectRoot, out var command))
    {
        return Results.BadRequest(new { ok = false, message = "Comando non consentito." });
    }

    var result = await command.RunAsync(cancellationToken);
    return Results.Ok(result);
});

app.Run();

static void ConfigureRenderPort(WebApplicationBuilder builder)
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (string.IsNullOrWhiteSpace(port))
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
        {
            return;
        }

        port = "5055";
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

static string ResolveProjectRoot(string contentRootPath)
{
    var sourceRoot = Path.GetFullPath(Path.Combine(contentRootPath, ".."));
    var workerProject = Path.Combine(sourceRoot, "CryptoSignalBot.Worker", "CryptoSignalBot.Worker.csproj");
    return File.Exists(workerProject) ? sourceRoot : contentRootPath;
}

static string ResolveWorkerSettingsPath(string contentRootPath)
{
    var candidates = new[]
    {
        Environment.GetEnvironmentVariable("CRYPTO_SIGNAL_BOT_WORKER_SETTINGS"),
        Path.Combine(contentRootPath, "..", "CryptoSignalBot.Worker", "appsettings.json"),
        Path.Combine(contentRootPath, "..", "worker", "appsettings.json"),
        Path.Combine(contentRootPath, "appsettings.json")
    };

    return candidates
        .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
        .Select(candidate => Path.GetFullPath(candidate!))
        .FirstOrDefault(File.Exists) ??
        Path.Combine(contentRootPath, "appsettings.json");
}

static string ResolveEnvironmentSettingsPath(string settingsPath, string environmentName)
{
    var directory = Path.GetDirectoryName(settingsPath) ?? "";
    var fileName = Path.GetFileNameWithoutExtension(settingsPath);
    return Path.Combine(directory, $"{fileName}.{environmentName}.json");
}

static object ToPaperTradeDto(PaperTradeResult result)
{
    return new
    {
        result.SignalId,
        result.Symbol,
        result.Timeframe,
        result.CreatedAt,
        result.EntryPrice,
        result.StopLoss,
        result.TakeProfit1,
        result.Score,
        result.SignalType,
        Outcome = result.Outcome.ToString(),
        result.ExitTime,
        result.ExitPrice,
        result.ReturnPercent
    };
}

static object ToPaperPortfolioTradeDto(PaperPortfolioTrade trade)
{
    return new
    {
        trade.SignalId,
        trade.Symbol,
        trade.Timeframe,
        trade.EntryTime,
        trade.EntryPrice,
        trade.Units,
        trade.Invested,
        trade.CashBefore,
        trade.CashAfter,
        trade.RemainingUnits,
        trade.EntryFee,
        trade.ExitFee,
        trade.TotalFees,
        trade.SlippageCost,
        trade.ExitTime,
        trade.ExitPrice,
        trade.CurrentPrice,
        trade.BreakEvenStop,
        Outcome = trade.Outcome.ToString(),
        trade.IsClosed,
        trade.CurrentValue,
        trade.ProfitLoss,
        trade.ProfitLossPercent
    };
}

static async Task UpdateBotSettingsAsync(
    string appsettingsPath,
    BotSettingsDto settings,
    CancellationToken cancellationToken)
{
    JsonNode root;
    await using (var stream = File.OpenRead(appsettingsPath))
    {
        root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? new JsonObject();
    }

    if (root is not JsonObject rootObject)
    {
        throw new InvalidOperationException("Worker appsettings.json must contain a JSON object.");
    }

    rootObject["Bot"] = new JsonObject
    {
        ["Symbols"] = ToJsonArray(settings.Symbols),
        ["Timeframes"] = ToJsonArray(settings.Timeframes),
        ["ReportTimeframes"] = ToJsonArray(settings.ReportTimeframes),
        ["MinScoreToNotify"] = settings.MinScoreToNotify,
        ["MaxReportSetups"] = settings.MaxReportSetups,
        ["DeduplicateReportMinutes"] = settings.DeduplicateReportMinutes,
        ["SignalDedupeMinutes"] = settings.SignalDedupeMinutes,
        ["RetainCandlesDays"] = settings.RetainCandlesDays,
        ["RetainSignalsDays"] = settings.RetainSignalsDays,
        ["DryRunOnly"] = settings.DryRunOnly,
        ["AccountBalance"] = settings.AccountBalance,
        ["RiskPercent"] = settings.RiskPercent,
        ["PaperPortfolioInitialBudget"] = settings.PaperPortfolioInitialBudget,
        ["PaperTradingFeePercent"] = settings.PaperTradingFeePercent,
        ["PaperTradingSlippagePercent"] = settings.PaperTradingSlippagePercent,
        ["PaperTradingTakeProfit1ExitPercent"] = settings.PaperTradingTakeProfit1ExitPercent
    };

    var json = rootObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    var tempPath = $"{appsettingsPath}.tmp";
    await File.WriteAllTextAsync(tempPath, json, cancellationToken);
    File.Move(tempPath, appsettingsPath, overwrite: true);
}

static JsonArray ToJsonArray(IEnumerable<string> values)
{
    return new JsonArray(values.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>());
}

internal static class DashboardPage
{
    public const string Html = """
<!doctype html>
<html lang="it">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>CryptoSignalBot Dashboard</title>
  <style>
    :root { color-scheme: light; --ink:#17202a; --muted:#607080; --line:#d9e1e8; --panel:#ffffff; --bg:#f5f7fa; --accent:#12715b; --warn:#b45f06; --bad:#a33a3a; }
    * { box-sizing: border-box; }
    body { margin:0; font-family: Segoe UI, Arial, sans-serif; background:var(--bg); color:var(--ink); }
    header { padding:22px 28px; border-bottom:1px solid var(--line); background:#fff; display:flex; align-items:center; justify-content:space-between; gap:16px; }
    h1 { margin:0; font-size:24px; font-weight:700; letter-spacing:0; }
    main { padding:24px 28px; display:grid; grid-template-columns:minmax(320px, 440px) 1fr; gap:24px; align-items:start; }
    .stack { display:grid; gap:24px; }
    section { background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:18px; }
    h2 { margin:0 0 14px; font-size:17px; }
    label { display:block; font-size:13px; color:var(--muted); margin:14px 0 6px; }
    input, textarea { width:100%; border:1px solid var(--line); border-radius:6px; padding:9px 10px; font:inherit; background:#fff; color:var(--ink); }
    input[type="checkbox"] { width:auto; transform:translateY(1px); }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:12px; }
    .toolbar { display:flex; gap:10px; align-items:center; }
    button { border:0; border-radius:6px; padding:10px 13px; font-weight:700; cursor:pointer; background:var(--accent); color:#fff; }
    button.secondary { background:#e9eef2; color:var(--ink); }
    button.warning { background:var(--warn); }
    button:disabled { opacity:.55; cursor:not-allowed; }
    .status { color:var(--muted); font-size:13px; }
    .cards { display:grid; grid-template-columns:repeat(4, minmax(120px, 1fr)); gap:12px; margin-bottom:18px; }
    .metric { border:1px solid var(--line); border-radius:8px; padding:12px; background:#fbfcfd; min-height:74px; }
    .metric strong { display:block; font-size:22px; line-height:1.2; margin-top:4px; }
    .metric .bad { color:var(--bad); }
    .metric .ok { color:var(--accent); }
    details.legend { border:1px solid var(--line); border-radius:8px; padding:12px; margin:0 0 16px; background:#fbfcfd; }
    details.legend summary { cursor:pointer; font-weight:700; }
    .legend-grid { display:grid; grid-template-columns:repeat(2, minmax(220px, 1fr)); gap:10px 18px; margin-top:12px; font-size:13px; }
    .legend-grid div { color:var(--muted); }
    .legend-grid strong { display:block; color:var(--ink); margin-bottom:2px; }
    .chart { width:100%; height:260px; border:1px solid var(--line); border-radius:8px; background:#fbfcfd; margin:14px 0 18px; }
    .chart path.line { fill:none; stroke:var(--accent); stroke-width:2.5; }
    .chart path.area { fill:rgba(18,113,91,.10); stroke:none; }
    .chart line.grid { stroke:#e5ebf0; stroke-width:1; }
    .chart text { fill:var(--muted); font-size:12px; }
    table { width:100%; border-collapse:collapse; font-size:13px; }
    th, td { padding:10px 8px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; }
    th { color:var(--muted); font-weight:600; }
    .score { font-weight:700; color:var(--accent); }
    .empty { color:var(--muted); padding:18px 0; }
    .pill { display:inline-block; padding:3px 7px; border-radius:999px; background:#eaf4f1; color:var(--accent); font-weight:700; }
    .warn { color:var(--warn); }
    pre { white-space:pre-wrap; word-break:break-word; background:#101820; color:#eef5f2; border-radius:8px; padding:12px; max-height:260px; overflow:auto; font-size:12px; }
    @media (max-width: 1100px) { .cards { grid-template-columns:repeat(2, minmax(120px, 1fr)); } }
    @media (max-width: 900px) { main { grid-template-columns:1fr; padding:16px; } header { padding:16px; align-items:flex-start; flex-direction:column; } }
  </style>
</head>
<body>
  <header>
    <div>
      <h1>CryptoSignalBot</h1>
      <div class="status" id="clock">Caricamento...</div>
    </div>
    <div class="toolbar">
      <button class="secondary" onclick="loadStatus()">Aggiorna</button>
      <button onclick="saveSettings()">Salva impostazioni</button>
    </div>
  </header>
  <main>
    <section>
      <h2>Impostazioni bot</h2>
      <label>Symbols</label>
      <textarea id="symbols" rows="3"></textarea>
      <label>Timeframes analisi</label>
      <input id="timeframes">
      <label>Timeframes report</label>
      <input id="reportTimeframes">
      <div class="grid">
        <div><label>Soglia alert</label><input id="minScore" type="number" min="0" max="10" step="0.1"></div>
        <div><label>Max setup report</label><input id="maxReportSetups" type="number" min="1" max="50"></div>
        <div><label>Dedupe report minuti</label><input id="dedupeReport" type="number" min="0"></div>
        <div><label>Dedupe segnali minuti</label><input id="dedupeSignal" type="number" min="0"></div>
        <div><label>Retain candele giorni</label><input id="retainCandles" type="number" min="1"></div>
        <div><label>Retain segnali giorni</label><input id="retainSignals" type="number" min="1"></div>
        <div><label>Capitale teorico</label><input id="accountBalance" type="number" min="0" step="0.01"></div>
        <div><label>Rischio per trade</label><input id="riskPercent" type="number" min="0" max="0.2" step="0.001"></div>
        <div><label>Budget test portfolio</label><input id="paperPortfolioInitialBudget" type="number" min="10" step="10"></div>
        <div><label>Fee paper per lato</label><input id="paperTradingFeePercent" type="number" min="0" max="0.05" step="0.0001"></div>
        <div><label>Slippage paper</label><input id="paperTradingSlippagePercent" type="number" min="0" max="0.05" step="0.0001"></div>
        <div><label>Quota venduta a TP1</label><input id="paperTradingTakeProfit1ExitPercent" type="number" min="0.1" max="0.9" step="0.05"></div>
      </div>
      <label><input id="dryRunOnly" type="checkbox"> Dry run only</label>
      <p class="status" id="saveStatus">Le password restano fuori da questa schermata.</p>
    </section>
    <div class="stack">
      <section>
        <h2>Azioni rapide</h2>
        <div class="toolbar">
          <button onclick="runCommand('report-watchlist')">Invia report ora</button>
          <button class="secondary" onclick="runCommand('paper-trade-report')">Paper report</button>
          <button class="secondary" onclick="runCommand('backtest-report')">Backtest</button>
          <button class="warning" onclick="runCommand('cleanup-db')">Cleanup DB</button>
          <button class="secondary" onclick="window.location.href='/api/export/signals.csv?days=30&take=1000'">Export CSV</button>
        </div>
        <p class="status" id="commandStatus">Pronto.</p>
        <pre id="commandOutput" hidden></pre>
      </section>
      <section>
        <h2>Stato</h2>
        <div class="cards">
          <div class="metric"><span class="status">API</span><strong id="apiState">...</strong></div>
          <div class="metric"><span class="status">Database</span><strong id="dbState">...</strong></div>
          <div class="metric"><span class="status">Segnali 7 giorni</span><strong id="signalCount">0</strong></div>
          <div class="metric"><span class="status">Ultimo segnale</span><strong id="latestSignal">-</strong></div>
        </div>
        <h2>Paper summary</h2>
        <div class="cards">
          <div class="metric"><span class="status">Trade valutati</span><strong id="paperTotal">0</strong></div>
          <div class="metric"><span class="status">Win rate</span><strong id="paperWinRate">0%</strong></div>
          <div class="metric"><span class="status">Wins / Losses</span><strong id="paperClosed">0 / 0</strong></div>
          <div class="metric"><span class="status">Open / Expired</span><strong id="paperOpen">0 / 0</strong></div>
        </div>
        <div id="paperTrades"></div>
        <h2>Portfolio test</h2>
        <details class="legend" open>
          <summary>Legenda semplice</summary>
          <div class="legend-grid">
            <div><strong>Budget iniziale</strong>Capitale finto da cui parte la simulazione. Non sono soldi reali.</div>
            <div><strong>Cash disponibile</strong>Soldi finti rimasti liberi dopo gli acquisti simulati.</div>
            <div><strong>Valore posizioni</strong>Valore attuale delle posizioni ancora aperte.</div>
            <div><strong>Equity simulata</strong>Cash disponibile + valore posizioni aperte. E' il valore totale del wallet finto.</div>
            <div><strong>Guadagno/perdita</strong>Differenza tra equity simulata e budget iniziale.</div>
            <div><strong>P/L realizzato</strong>Profitto o perdita dei trade gia' chiusi. Le posizioni aperte non sono incluse.</div>
            <div><strong>Win rate</strong>Percentuale dei trade chiusi in profitto. Con pochi trade puo' essere ingannevole.</div>
            <div><strong>Profit factor</strong>Rapporto tra profitti lordi e perdite lorde. Sopra 1 significa che i trade chiusi stanno producendo piu' profitto che perdita.</div>
            <div><strong>Expectancy</strong>Guadagno o perdita media attesa per trade chiuso, in base ai trade gia' simulati.</div>
            <div><strong>Drawdown massimo</strong>La peggior discesa del wallet dal suo massimo precedente. Serve a capire quanto soffre il portafoglio.</div>
            <div><strong>Open</strong>Trade ancora aperto: non ha ancora preso stop loss, take profit o scadenza.</div>
            <div><strong>Take profit</strong>Uscita in guadagno perche' il prezzo ha raggiunto il primo obiettivo.</div>
            <div><strong>TP1 / TP2</strong>TP1 vende una parte della posizione. TP2 chiude il resto se il movimento continua.</div>
            <div><strong>Stop loss</strong>Uscita in perdita controllata perche' il prezzo ha rotto il livello di invalidazione.</div>
            <div><strong>Break-even</strong>Dopo TP1 lo stop del resto viene spostato vicino al prezzo di ingresso, per proteggere il trade.</div>
            <div><strong>Fee e slippage</strong>Costi simulati: commissioni e piccolo peggioramento del prezzo di ingresso/uscita.</div>
            <div><strong>Chiuso a scadenza</strong>Il trade non ha preso ne' target ne' stop entro la finestra testata, quindi viene chiuso all'ultima candela disponibile.</div>
            <div><strong>Cash wallet</strong>Saldo prima del trade e saldo dopo quel trade. Serve a vedere che il budget non riparte da capo.</div>
          </div>
        </details>
        <div class="cards">
          <div class="metric"><span class="status">Budget iniziale</span><strong id="portfolioBudget">0</strong></div>
          <div class="metric"><span class="status">Cash disponibile</span><strong id="portfolioCash">0</strong></div>
          <div class="metric"><span class="status">Valore posizioni</span><strong id="portfolioOpenValue">0</strong></div>
          <div class="metric"><span class="status">Equity simulata</span><strong id="portfolioEquity">0</strong></div>
          <div class="metric"><span class="status">Guadagno/perdita</span><strong id="portfolioPnl">0</strong></div>
          <div class="metric"><span class="status">P/L realizzato</span><strong id="portfolioRealizedPnl">0</strong></div>
          <div class="metric"><span class="status">P/L aperto</span><strong id="portfolioUnrealizedPnl">0</strong></div>
          <div class="metric"><span class="status">Costi simulati</span><strong id="portfolioFees">0</strong></div>
          <div class="metric"><span class="status">Capitale impegnato</span><strong id="portfolioCapitalAtWork">0%</strong></div>
          <div class="metric"><span class="status">Trade chiusi / aperti</span><strong id="portfolioClosedOpen">0 / 0</strong></div>
          <div class="metric"><span class="status">Win rate</span><strong id="portfolioWinRate">0%</strong></div>
          <div class="metric"><span class="status">Periodo replay</span><strong id="portfolioPeriod">-</strong></div>
          <div class="metric"><span class="status">Miglior / peggior trade</span><strong id="portfolioBestWorst">0 / 0</strong></div>
          <div class="metric"><span class="status">Profit factor</span><strong id="portfolioProfitFactor">0</strong></div>
          <div class="metric"><span class="status">Expectancy</span><strong id="portfolioExpectancy">0</strong></div>
          <div class="metric"><span class="status">Drawdown max</span><strong id="portfolioDrawdown">0</strong></div>
          <div class="metric"><span class="status">Media win / loss</span><strong id="portfolioAvgWinLoss">0 / 0</strong></div>
        </div>
        <svg id="portfolioChart" class="chart" viewBox="0 0 900 260" role="img" aria-label="Andamento equity portfolio"></svg>
        <div id="portfolioTrades"></div>
      </section>
      <section>
        <h2>Task schedulate</h2>
        <div id="tasks"></div>
      </section>
      <section>
        <h2>Segnali recenti</h2>
        <div id="signals"></div>
      </section>
    </div>
  </main>
  <script>
    let currentSettings = null;
    const splitList = value => value.split(/[,\n]/).map(x => x.trim()).filter(Boolean);
    const joinList = value => (value || []).join(", ");
    const text = value => value === null || value === undefined || value === "" ? "-" : String(value);
    const money = value => value === null || value === undefined ? "-" : Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 });
    const outcomeLabel = value => ({
      TakeProfit1: "Take profit",
      TakeProfit2: "Take profit 2",
      StopLoss: "Stop loss",
      Expired: "Chiuso a scadenza",
      Open: "Open",
      Invalid: "Non valido"
    })[value] || value;
    const escapeHtml = value => text(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

    async function loadStatus() {
      const response = await fetch("/api/status");
      const data = await response.json();
      currentSettings = data.settings;
      document.getElementById("clock").textContent = `UTC ${new Date(data.utcNow).toLocaleString()}`;
      apiState.textContent = data.status === "ok" ? "OK" : "Degraded";
      apiState.className = data.status === "ok" ? "ok" : "bad";
      dbState.textContent = data.database?.status === "ok" ? "OK" : "Errore";
      dbState.className = data.database?.status === "ok" ? "ok" : "bad";
      signalCount.textContent = data.database?.signalCountLast7Days ?? 0;
      latestSignal.textContent = data.database?.latestSignalAt ? new Date(data.database.latestSignalAt).toLocaleString() : "-";
      bindSettings(currentSettings);
      await Promise.all([loadRecentSignals(), loadPaperSummary(), loadPaperPortfolio(), loadTaskStatus()]);
    }

    async function loadRecentSignals() {
      const response = await fetch("/api/signals/recent?days=7&take=30");
      if (!response.ok) {
        document.getElementById("signals").innerHTML = '<div class="empty">Segnali non disponibili.</div>';
        return;
      }
      const data = await response.json();
      renderSignals(data.signals || []);
    }

    async function loadPaperSummary() {
      const response = await fetch("/api/paper/summary?maxSignals=100&maxFutureCandles=24");
      if (!response.ok) {
        paperTrades.innerHTML = '<div class="empty">Paper summary non disponibile.</div>';
        return;
      }
      const data = await response.json();
      paperTotal.textContent = data.total ?? 0;
      paperWinRate.textContent = `${data.winRate ?? 0}%`;
      paperClosed.textContent = `${data.wins ?? 0} / ${data.losses ?? 0}`;
      paperOpen.textContent = `${data.openCount ?? 0} / ${data.expiredCount ?? 0}`;
      renderPaperTrades(data.recentClosedTrades || []);
    }

    async function loadTaskStatus() {
      const response = await fetch("/api/tasks/status");
      if (!response.ok) {
        tasks.innerHTML = '<div class="empty">Stato task non disponibile.</div>';
        return;
      }
      const data = await response.json();
      renderTasks(data.tasks || []);
    }

    function bindSettings(settings) {
      symbols.value = joinList(settings.symbols);
      timeframes.value = joinList(settings.timeframes);
      reportTimeframes.value = joinList(settings.reportTimeframes);
      minScore.value = settings.minScoreToNotify;
      maxReportSetups.value = settings.maxReportSetups;
      dedupeReport.value = settings.deduplicateReportMinutes;
      dedupeSignal.value = settings.signalDedupeMinutes;
      retainCandles.value = settings.retainCandlesDays;
      retainSignals.value = settings.retainSignalsDays;
      accountBalance.value = settings.accountBalance;
      riskPercent.value = settings.riskPercent;
      paperPortfolioInitialBudget.value = settings.paperPortfolioInitialBudget;
      paperTradingFeePercent.value = settings.paperTradingFeePercent;
      paperTradingSlippagePercent.value = settings.paperTradingSlippagePercent;
      paperTradingTakeProfit1ExitPercent.value = settings.paperTradingTakeProfit1ExitPercent;
      dryRunOnly.checked = settings.dryRunOnly;
    }

    async function saveSettings() {
      const payload = {
        symbols: splitList(symbols.value),
        timeframes: splitList(timeframes.value),
        reportTimeframes: splitList(reportTimeframes.value),
        minScoreToNotify: Number(minScore.value),
        maxReportSetups: Number(maxReportSetups.value),
        deduplicateReportMinutes: Number(dedupeReport.value),
        signalDedupeMinutes: Number(dedupeSignal.value),
        retainCandlesDays: Number(retainCandles.value),
        retainSignalsDays: Number(retainSignals.value),
        dryRunOnly: dryRunOnly.checked,
        accountBalance: Number(accountBalance.value),
        riskPercent: Number(riskPercent.value),
        paperPortfolioInitialBudget: Number(paperPortfolioInitialBudget.value),
        paperTradingFeePercent: Number(paperTradingFeePercent.value),
        paperTradingSlippagePercent: Number(paperTradingSlippagePercent.value),
        paperTradingTakeProfit1ExitPercent: Number(paperTradingTakeProfit1ExitPercent.value)
      };
      saveStatus.textContent = "Salvataggio...";
      const response = await fetch("/api/settings/bot", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!response.ok) {
        saveStatus.textContent = "Errore salvataggio.";
        return;
      }
      saveStatus.textContent = "Impostazioni salvate in appsettings.json.";
      await loadStatus();
    }

    async function runCommand(commandName) {
      const buttons = Array.from(document.querySelectorAll("button"));
      buttons.forEach(button => button.disabled = true);
      commandStatus.textContent = `Esecuzione ${commandName}...`;
      commandOutput.hidden = true;
      try {
        const response = await fetch(`/api/commands/${commandName}`, { method: "POST" });
        const data = await response.json();
        commandStatus.textContent = data.ok ? "Comando completato." : "Comando terminato con errore.";
        commandOutput.hidden = false;
        commandOutput.textContent = [data.output, data.error].filter(Boolean).join("\n\n");
        await Promise.all([loadRecentSignals(), loadPaperSummary(), loadPaperPortfolio()]);
      } catch {
        commandStatus.textContent = "Errore durante l'esecuzione.";
      } finally {
        buttons.forEach(button => button.disabled = false);
      }
    }

    function renderSignals(signals) {
      if (!signals.length) {
        document.getElementById("signals").innerHTML = '<div class="empty">Nessun segnale negli ultimi 7 giorni.</div>';
        return;
      }
      const rows = signals.map(signal => `
        <tr>
          <td><strong>${escapeHtml(signal.symbol)}</strong><br>${escapeHtml(signal.timeframe)}</td>
          <td><span class="pill">${escapeHtml(signal.signalType)}</span><br><span class="status">${new Date(signal.createdAt).toLocaleString()}</span></td>
          <td class="score">${escapeHtml(signal.score)}/10</td>
          <td>${money(signal.price)}<br><span class="status">SL ${money(signal.stopLoss)} TP ${money(signal.takeProfit1)}</span></td>
          <td>${escapeHtml(signal.summary)}</td>
        </tr>`).join("");
      document.getElementById("signals").innerHTML = `<table><thead><tr><th>Asset</th><th>Tipo</th><th>Score</th><th>Prezzo</th><th>Note</th></tr></thead><tbody>${rows}</tbody></table>`;
    }

    function renderPaperTrades(trades) {
      if (!trades.length) {
        paperTrades.innerHTML = '<div class="empty">Nessun trade chiuso disponibile.</div>';
        return;
      }
      const rows = trades.map(trade => `
        <tr>
          <td><strong>${escapeHtml(trade.symbol)}</strong><br>${escapeHtml(trade.timeframe)}</td>
          <td><span class="pill">${escapeHtml(outcomeLabel(trade.outcome))}</span><br><span class="status">${trade.exitTime ? new Date(trade.exitTime).toLocaleString() : "-"}</span></td>
          <td>${money(trade.entryPrice)}<br><span class="status">exit ${money(trade.exitPrice)}</span></td>
          <td class="${Number(trade.returnPercent) >= 0 ? "score" : "warn"}">${escapeHtml(trade.returnPercent)}%</td>
        </tr>`).join("");
      paperTrades.innerHTML = `<table><thead><tr><th>Asset</th><th>Outcome</th><th>Prezzo</th><th>Return</th></tr></thead><tbody>${rows}</tbody></table>`;
    }

    async function loadPaperPortfolio() {
      const response = await fetch(`/api/paper/portfolio?initialBudget=${encodeURIComponent(currentSettings?.paperPortfolioInitialBudget ?? 500)}&maxSignals=100&maxFutureCandles=24`);
      if (!response.ok) {
        portfolioTrades.innerHTML = '<div class="empty">Portfolio test non disponibile.</div>';
        return;
      }
      const data = await response.json();
      portfolioBudget.textContent = money(data.initialBudget);
      portfolioCash.textContent = money(data.cash);
      portfolioOpenValue.textContent = money(data.openPositionValue);
      portfolioEquity.textContent = money(data.equity);
      portfolioPnl.textContent = `${money(data.profitLoss)} (${data.profitLossPercent ?? 0}%)`;
      portfolioPnl.className = Number(data.profitLoss) >= 0 ? "ok" : "bad";
      portfolioRealizedPnl.textContent = money(data.realizedProfitLoss);
      portfolioRealizedPnl.className = Number(data.realizedProfitLoss) >= 0 ? "ok" : "bad";
      portfolioUnrealizedPnl.textContent = money(data.unrealizedProfitLoss);
      portfolioUnrealizedPnl.className = Number(data.unrealizedProfitLoss) >= 0 ? "ok" : "bad";
      portfolioFees.textContent = money(data.totalFees);
      portfolioCapitalAtWork.textContent = `${data.capitalAtWorkPercent ?? 0}%`;
      portfolioClosedOpen.textContent = `${data.closedCount ?? 0} / ${data.openCount ?? 0}`;
      portfolioWinRate.textContent = `${data.winRate ?? 0}%`;
      portfolioPeriod.textContent = data.firstTradeAt
        ? `${new Date(data.firstTradeAt).toLocaleDateString()} - ${new Date(data.lastTradeAt).toLocaleDateString()}`
        : "-";
      portfolioBestWorst.textContent = `${money(data.bestTradeProfitLoss)} / ${money(data.worstTradeProfitLoss)}`;
      portfolioProfitFactor.textContent = Number(data.profitFactor ?? 0).toLocaleString(undefined, { maximumFractionDigits: 4 });
      portfolioExpectancy.textContent = `${money(data.expectancy)} (${data.expectancyPercent ?? 0}%)`;
      portfolioDrawdown.textContent = `${money(data.maxDrawdown)} (${data.maxDrawdownPercent ?? 0}%)`;
      portfolioAvgWinLoss.textContent = `${money(data.averageWin)} / ${money(data.averageLoss)}`;
      renderEquityChart(data.equityCurve || []);
      renderPortfolioTrades(data.trades || []);
    }

    function renderEquityChart(points) {
      const svg = portfolioChart;
      const width = 900;
      const height = 260;
      const pad = { left: 58, right: 18, top: 20, bottom: 34 };
      svg.innerHTML = "";

      if (!points.length) {
        svg.innerHTML = `<text x="24" y="42">Nessun dato per il grafico.</text>`;
        return;
      }

      const values = points.map(point => Number(point.equity));
      let min = Math.min(...values);
      let max = Math.max(...values);
      if (min === max) {
        min -= 1;
        max += 1;
      }
      const plotWidth = width - pad.left - pad.right;
      const plotHeight = height - pad.top - pad.bottom;
      const x = index => pad.left + (points.length === 1 ? 0 : index / (points.length - 1) * plotWidth);
      const y = value => pad.top + (max - value) / (max - min) * plotHeight;
      const line = points.map((point, index) => `${index === 0 ? "M" : "L"}${x(index).toFixed(2)},${y(Number(point.equity)).toFixed(2)}`).join(" ");
      const area = `${line} L${x(points.length - 1).toFixed(2)},${height - pad.bottom} L${pad.left},${height - pad.bottom} Z`;

      const gridValues = [min, (min + max) / 2, max];
      const grid = gridValues.map(value => {
        const yy = y(value);
        return `<line class="grid" x1="${pad.left}" x2="${width - pad.right}" y1="${yy}" y2="${yy}"></line><text x="10" y="${yy + 4}">${money(value)}</text>`;
      }).join("");
      const circles = points.map((point, index) => {
        const cls = Number(point.profitLoss) >= 0 ? "var(--accent)" : "var(--bad)";
        return `<circle cx="${x(index)}" cy="${y(Number(point.equity))}" r="3.5" fill="${cls}"><title>${escapeHtml(point.label)} ${new Date(point.time).toLocaleString()} equity ${money(point.equity)}</title></circle>`;
      }).join("");

      svg.innerHTML = `
        ${grid}
        <path class="area" d="${area}"></path>
        <path class="line" d="${line}"></path>
        ${circles}
        <text x="${pad.left}" y="${height - 10}">${new Date(points[0].time).toLocaleDateString()}</text>
        <text x="${width - 130}" y="${height - 10}">${new Date(points[points.length - 1].time).toLocaleDateString()}</text>`;
    }

    function renderPortfolioTrades(trades) {
      if (!trades.length) {
        portfolioTrades.innerHTML = '<div class="empty">Nessun acquisto simulato disponibile.</div>';
        return;
      }
      const rows = trades.map(trade => `
        <tr>
          <td><strong>${escapeHtml(trade.symbol)}</strong><br>${escapeHtml(trade.timeframe)}</td>
          <td><span class="pill">${escapeHtml(outcomeLabel(trade.outcome))}</span><br><span class="status">${new Date(trade.entryTime).toLocaleString()}</span></td>
          <td>${money(trade.invested)}<br><span class="status">${money(trade.remainingUnits)} / ${money(trade.units)} units</span></td>
          <td>${money(trade.cashBefore)}<br><span class="status">dopo ${money(trade.cashAfter)}</span></td>
          <td>${money(trade.entryPrice)}<br><span class="status">exit ${money(trade.exitPrice ?? trade.currentPrice)}</span></td>
          <td class="${Number(trade.profitLoss) >= 0 ? "score" : "warn"}">${money(trade.profitLoss)} (${trade.profitLossPercent}%)<br><span class="status">fee ${money(trade.totalFees)}</span></td>
        </tr>`).join("");
      portfolioTrades.innerHTML = `<table><thead><tr><th>Asset</th><th>Stato</th><th>Investito</th><th>Cash wallet</th><th>Prezzi</th><th>P/L</th></tr></thead><tbody>${rows}</tbody></table>`;
    }

    function renderTasks(items) {
      if (!items.length) {
        tasks.innerHTML = '<div class="empty">Nessuna task configurata.</div>';
        return;
      }
      const rows = items.map(task => `
        <tr>
          <td><strong>${escapeHtml(task.name)}</strong></td>
          <td>${task.exists ? '<span class="pill">Installata</span>' : '<span class="warn">Mancante</span>'}</td>
          <td>${escapeHtml(task.state)}</td>
          <td>${task.lastRunTime ? new Date(task.lastRunTime).toLocaleString() : "-"}</td>
          <td>${task.nextRunTime ? new Date(task.nextRunTime).toLocaleString() : "-"}</td>
          <td>${escapeHtml(task.lastTaskResult)}</td>
        </tr>`).join("");
      tasks.innerHTML = `<table><thead><tr><th>Task</th><th>Stato</th><th>Runtime</th><th>Ultimo run</th><th>Prossimo run</th><th>Codice</th></tr></thead><tbody>${rows}</tbody></table>`;
    }

    loadStatus();
  </script>
</body>
</html>
""";
}
