using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoSignalBot.Application;
using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Dashboard;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Infrastructure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var startedAt = DateTimeOffset.UtcNow;
var projectRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));
var workerSettingsPath = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    "..",
    "CryptoSignalBot.Worker",
    "appsettings.json"));

builder.Configuration.AddJsonFile(workerSettingsPath, optional: false, reloadOnChange: true);
builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("Bot"));
builder.Services.Configure<BinanceSettings>(builder.Configuration.GetSection("Binance"));
builder.Services.Configure<CoinGeckoSettings>(builder.Configuration.GetSection("CoinGecko"));
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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
        ["RiskPercent"] = settings.RiskPercent
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
        <div><label>Balance teorico</label><input id="accountBalance" type="number" min="0" step="0.01"></div>
        <div><label>Rischio per trade</label><input id="riskPercent" type="number" min="0" max="0.2" step="0.001"></div>
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
      await Promise.all([loadRecentSignals(), loadPaperSummary()]);
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
        riskPercent: Number(riskPercent.value)
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
        await Promise.all([loadRecentSignals(), loadPaperSummary()]);
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
          <td><span class="pill">${escapeHtml(trade.outcome)}</span><br><span class="status">${trade.exitTime ? new Date(trade.exitTime).toLocaleString() : "-"}</span></td>
          <td>${money(trade.entryPrice)}<br><span class="status">exit ${money(trade.exitPrice)}</span></td>
          <td class="${Number(trade.returnPercent) >= 0 ? "score" : "warn"}">${escapeHtml(trade.returnPercent)}%</td>
        </tr>`).join("");
      paperTrades.innerHTML = `<table><thead><tr><th>Asset</th><th>Outcome</th><th>Prezzo</th><th>Return</th></tr></thead><tbody>${rows}</tbody></table>`;
    }

    loadStatus();
  </script>
</body>
</html>
""";
}
