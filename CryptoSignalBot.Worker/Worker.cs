using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Backtesting;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.Signals;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BotSettings> _botSettings;
    private readonly string[] _args;

    public Worker(
        ILogger<Worker> logger,
        IHostApplicationLifetime lifetime,
        IServiceScopeFactory scopeFactory,
        IOptions<BotSettings> botSettings,
        IHostEnvironment environment)
    {
        _logger = logger;
        _lifetime = lifetime;
        _scopeFactory = scopeFactory;
        _botSettings = botSettings;
        _args = Environment.GetCommandLineArgs();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase) &&
            _args.Contains("notifications", StringComparer.OrdinalIgnoreCase))
        {
            await SendNotificationSmokeTestAsync(stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        var analyzeOnceIndex = Array.FindIndex(_args, arg => string.Equals(arg, "--analyze-once", StringComparison.OrdinalIgnoreCase));
        if (analyzeOnceIndex >= 0)
        {
            var symbol = GetArgOrDefault(analyzeOnceIndex + 1, _botSettings.Value.Symbols.FirstOrDefault() ?? "BTCUSDT");
            var timeframe = GetArgOrDefault(analyzeOnceIndex + 2, _botSettings.Value.Timeframes.FirstOrDefault() ?? "1h");

            await AnalyzeSingleAsync(symbol, timeframe, sendNotification: true, stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        if (_args.Contains("--analyze-watchlist", StringComparer.OrdinalIgnoreCase))
        {
            await AnalyzeWatchlistAsync(sendIndividualAlerts: true, stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        if (_args.Contains("--report-watchlist", StringComparer.OrdinalIgnoreCase))
        {
            await ReportWatchlistAsync(stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        if (_args.Contains("--paper-trade-report", StringComparer.OrdinalIgnoreCase))
        {
            await PaperTradeReportAsync(stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        if (_args.Contains("--backtest-report", StringComparer.OrdinalIgnoreCase))
        {
            await BacktestReportAsync(stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        if (_args.Contains("--cleanup-db", StringComparer.OrdinalIgnoreCase))
        {
            await CleanupDatabaseAsync(stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task SendNotificationSmokeTestAsync(CancellationToken cancellationToken)
    {
        var signal = new Signal(
            "BTCUSDT",
            "1h",
            DateTimeOffset.UtcNow,
            64200m,
            8.1m,
            SignalType.BuyWatch,
            RiskLevel.Medium,
            62659.20m,
            66768m,
            69015m,
            1.67m,
            "Smoke test notifiche CryptoSignalBot: se leggi questo messaggio, Gmail/Telegram sono configurati correttamente.",
            [
                new RuleResult("Smoke test", 0m, RuleResultType.Pass, "Messaggio generato manualmente per verificare le notifiche.")
            ]);

        _logger.LogInformation("Sending notification smoke test.");
        using var scope = _scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendSignalAsync(signal, cancellationToken);
        _logger.LogInformation("Notification smoke test completed.");
    }

    private async Task AnalyzeWatchlistAsync(bool sendIndividualAlerts, CancellationToken cancellationToken)
    {
        var symbols = DistinctOrDefault(_botSettings.Value.Symbols, ["BTCUSDT"]);
        var timeframes = DistinctOrDefault(_botSettings.Value.Timeframes, ["1h"]);

        _logger.LogInformation("Analyze watchlist started: {SymbolCount} symbols, {TimeframeCount} timeframes.", symbols.Length, timeframes.Length);

        var analyzed = 0;
        var notified = 0;
        var globalMarketData = await LoadGlobalMarketDataForRunAsync(cancellationToken);

        foreach (var timeframe in timeframes)
        {
            foreach (var symbol in symbols)
            {
                var signal = await AnalyzeSingleAsync(
                    symbol,
                    timeframe,
                    sendNotification: sendIndividualAlerts,
                    cancellationToken,
                    globalMarketData,
                    useProvidedGlobalMarketData: true);
                if (signal is null)
                {
                    continue;
                }

                analyzed++;
                if (signal.Score >= _botSettings.Value.MinScoreToNotify)
                {
                    notified++;
                }
            }
        }

        _logger.LogInformation("Analyze watchlist completed: {Analyzed} analyzed, {Notified} notifications sent.", analyzed, notified);
    }

    private async Task ReportWatchlistAsync(CancellationToken cancellationToken)
    {
        var symbols = DistinctOrDefault(_botSettings.Value.Symbols, ["BTCUSDT"]);
        var timeframes = _botSettings.Value.ReportTimeframes.Length > 0
            ? DistinctOrDefault(_botSettings.Value.ReportTimeframes, ["1h", "4h", "1d"])
            : ["1h", "4h", "1d"];
        var signals = new List<Signal>();
        var forceReport = _args.Contains("--force-report", StringComparer.OrdinalIgnoreCase);
        var sendEmptyReport = _args.Contains("--send-empty-report", StringComparer.OrdinalIgnoreCase);
        var reportStartedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("Report watchlist started: {SymbolCount} symbols, {TimeframeCount} timeframes.", symbols.Length, timeframes.Length);

        using var reportScope = _scopeFactory.CreateScope();
        var persistenceService = reportScope.ServiceProvider.GetRequiredService<IPersistenceService>();
        var globalMarketDataService = reportScope.ServiceProvider.GetService<IGlobalMarketDataService>();
        var globalMarketData = await GetGlobalMarketDataAsync(globalMarketDataService, cancellationToken);
        var dedupeWindow = TimeSpan.FromMinutes(Math.Max(0, _botSettings.Value.DeduplicateReportMinutes));
        var recentSignals = forceReport || dedupeWindow == TimeSpan.Zero
            ? []
            : await persistenceService.GetSignalsSinceAsync(reportStartedAt - dedupeWindow, cancellationToken);
        var recentKeys = recentSignals
            .Where(signal => signal.CreatedAt < reportStartedAt)
            .Select(ReportDedupeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var timeframe in timeframes)
        {
            foreach (var symbol in symbols)
            {
                var signal = await AnalyzeSingleAsync(
                    symbol,
                    timeframe,
                    sendNotification: false,
                    cancellationToken,
                    globalMarketData,
                    useProvidedGlobalMarketData: true);
                if (signal is not null)
                {
                    signals.Add(signal);
                }
            }
        }

        var filteredSignals = signals
            .Where(signal => signal.Score < _botSettings.Value.MinScoreToNotify || !recentKeys.Contains(ReportDedupeKey(signal)))
            .ToArray();
        var suppressed = signals.Count - filteredSignals.Length;
        var notificationService = reportScope.ServiceProvider.GetRequiredService<INotificationService>();
        var report = new WatchlistReport(
            DateTimeOffset.UtcNow,
            _botSettings.Value.MinScoreToNotify,
            filteredSignals,
            signals.Count,
            _botSettings.Value.MaxReportSetups,
            suppressed);

        if (report.NotifiableSignals.Count == 0 && !sendEmptyReport && !forceReport)
        {
            _logger.LogInformation(
                "Report skipped: no fresh setup above threshold. {Suppressed} duplicates suppressed.",
                suppressed);
            return;
        }

        await notificationService.SendReportAsync(report, cancellationToken);

        _logger.LogInformation(
            "Report watchlist completed: {Analyzed} analyzed, {Alerts} alerts in report, {Suppressed} duplicates suppressed.",
            signals.Count,
            report.NotifiableSignals.Count,
            suppressed);
    }

    private async Task<Signal?> AnalyzeSingleAsync(
        string symbol,
        string timeframe,
        bool sendNotification,
        CancellationToken cancellationToken,
        GlobalMarketData? globalMarketData = null,
        bool useProvidedGlobalMarketData = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataService>();
        var indicatorEngine = scope.ServiceProvider.GetRequiredService<IIndicatorEngine>();
        var marketContextEngine = scope.ServiceProvider.GetRequiredService<IMarketContextEngine>();
        var globalMarketDataService = scope.ServiceProvider.GetService<IGlobalMarketDataService>();
        var riskEngine = scope.ServiceProvider.GetRequiredService<IRiskEngine>();
        var signalEngine = scope.ServiceProvider.GetRequiredService<ISignalEngine>();
        var persistenceService = scope.ServiceProvider.GetRequiredService<IPersistenceService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        _logger.LogInformation("Analyze once started for {Symbol} {Timeframe}.", symbol, timeframe);

        var candles = await marketData.GetCandlesAsync(symbol, timeframe, 250, cancellationToken);
        if (candles.Count == 0)
        {
            _logger.LogWarning("No candles returned for {Symbol} {Timeframe}.", symbol, timeframe);
            return null;
        }

        var btcCandles = string.Equals(symbol, "BTCUSDT", StringComparison.OrdinalIgnoreCase)
            ? candles
            : await marketData.GetCandlesAsync("BTCUSDT", timeframe, 250, cancellationToken);

        var indicators = indicatorEngine.Calculate(candles);
        globalMarketData = useProvidedGlobalMarketData
            ? globalMarketData
            : await GetGlobalMarketDataAsync(globalMarketDataService, cancellationToken);
        var marketContext = marketContextEngine.Evaluate(btcCandles, candles, globalMarketData);
        var latest = candles.OrderBy(candle => candle.OpenTime).Last();
        var atr = indicators.Atr14 ?? latest.ClosePrice * 0.02m;
        var riskPlan = riskEngine.CreatePlan(
            latest.ClosePrice,
            atr,
            _botSettings.Value.AccountBalance,
            _botSettings.Value.RiskPercent);

        var signal = signalEngine.Analyze(
            symbol.ToUpperInvariant(),
            timeframe,
            latest.ClosePrice,
            indicators,
            marketContext,
            riskPlan);

        await persistenceService.SaveCandlesAsync(candles, cancellationToken);
        await persistenceService.SaveSignalAsync(signal, cancellationToken);

        _logger.LogInformation(
            "Analyze once completed for {Symbol} {Timeframe}: price {Price}, score {Score}, signal {SignalType}.",
            signal.Symbol,
            signal.Timeframe,
            signal.Price,
            signal.Score,
            signal.SignalType);

        if (sendNotification && signal.Score >= _botSettings.Value.MinScoreToNotify)
        {
            await notificationService.SendSignalAsync(signal, cancellationToken);
            _logger.LogInformation("Notification sent for {Symbol} with score {Score}.", signal.Symbol, signal.Score);
        }
        else if (sendNotification)
        {
            _logger.LogInformation(
                "Notification skipped for {Symbol}: score {Score} below threshold {Threshold}.",
                signal.Symbol,
                signal.Score,
                _botSettings.Value.MinScoreToNotify);
        }

        return signal;
    }

    private async Task<GlobalMarketData?> LoadGlobalMarketDataForRunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var globalMarketDataService = scope.ServiceProvider.GetService<IGlobalMarketDataService>();
        return await GetGlobalMarketDataAsync(globalMarketDataService, cancellationToken);
    }

    private async Task<GlobalMarketData?> GetGlobalMarketDataAsync(
        IGlobalMarketDataService? globalMarketDataService,
        CancellationToken cancellationToken)
    {
        if (globalMarketDataService is null)
        {
            return null;
        }

        try
        {
            return await globalMarketDataService.GetGlobalMarketDataAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or InvalidOperationException or KeyNotFoundException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "CoinGecko global market context unavailable; continuing with candle-only context.");
            return null;
        }
    }

    private async Task PaperTradeReportAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var persistenceService = scope.ServiceProvider.GetRequiredService<IPersistenceService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var report = await persistenceService.BuildPaperTradeReportAsync(500, 120, cancellationToken);

        _logger.LogInformation(
            "Paper trade report: results {Count}, closed {Closed}, wins {Wins}, losses {Losses}, open {Open}, expired {Expired}, win rate {WinRate}%.",
            report.Results.Count,
            report.ClosedCount,
            report.Wins,
            report.Losses,
            report.OpenCount,
            report.ExpiredCount,
            report.WinRate);

        foreach (var result in report.RecentClosedTrades(20))
        {
            _logger.LogInformation(
                "Paper closed {Symbol} {Timeframe} {SignalType} score {Score}: {Outcome}, entry {EntryPrice}, exit {ExitPrice}, return {ReturnPercent}%.",
                result.Symbol,
                result.Timeframe,
                result.SignalType,
                result.Score,
                result.Outcome,
                result.EntryPrice,
                result.ExitPrice,
                result.ReturnPercent);
        }

        await notificationService.SendPaperTradeReportAsync(report, cancellationToken);
    }

    private async Task BacktestReportAsync(CancellationToken cancellationToken)
    {
        var symbols = DistinctOrDefault(_botSettings.Value.Symbols, ["BTCUSDT"]);
        var timeframes = DistinctOrDefault(_botSettings.Value.Timeframes, ["1h"]);
        var options = new BacktestOptions(
            symbols,
            timeframes,
            MaxCandles: 750,
            WarmupCandles: 200,
            MaxFutureCandles: 24,
            MinScore: _botSettings.Value.MinScoreToNotify,
            AccountBalance: _botSettings.Value.AccountBalance,
            RiskPercent: _botSettings.Value.RiskPercent);

        using var scope = _scopeFactory.CreateScope();
        var backtestService = scope.ServiceProvider.GetRequiredService<IBacktestService>();
        var report = await backtestService.RunAsync(options, cancellationToken);

        _logger.LogInformation(
            "Backtest report: symbols {SymbolCount}, setups {Setups}, closed {Closed}, wins {Wins}, losses {Losses}, win rate {WinRate}%, average return {AverageReturnPercent}%.",
            report.Symbols.Count,
            report.TestedSetups,
            report.ClosedCount,
            report.Wins,
            report.Losses,
            report.WinRate,
            report.AverageReturnPercent);

        foreach (var symbolReport in report.Symbols)
        {
            _logger.LogInformation(
                "Backtest {Symbol} {Timeframe}: candles {Candles}, evaluated bars {EvaluatedBars}, setups {Setups}, closed {Closed}, win rate {WinRate}%.",
                symbolReport.Symbol,
                symbolReport.Timeframe,
                symbolReport.CandleCount,
                symbolReport.EvaluatedBars,
                symbolReport.TestedSetups,
                symbolReport.ClosedCount,
                symbolReport.WinRate);
        }

        foreach (var result in report.Results.Take(20))
        {
            _logger.LogInformation(
                "Backtest trade {Symbol} {Timeframe} {SignalType} score {Score}: {Outcome}, return {ReturnPercent}%.",
                result.Symbol,
                result.Timeframe,
                result.SignalType,
                result.Score,
                result.Outcome,
                result.ReturnPercent);
        }
    }

    private async Task CleanupDatabaseAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var persistenceService = scope.ServiceProvider.GetRequiredService<IPersistenceService>();
        var now = DateTimeOffset.UtcNow;
        var deleted = await persistenceService.CleanupDatabaseAsync(
            now.AddDays(-Math.Max(1, _botSettings.Value.RetainCandlesDays)),
            now.AddDays(-Math.Max(1, _botSettings.Value.RetainSignalsDays)),
            cancellationToken);

        _logger.LogInformation(
            "Database cleanup completed: {Deleted} old rows removed. Retention candles {CandleDays} days, signals {SignalDays} days.",
            deleted,
            _botSettings.Value.RetainCandlesDays,
            _botSettings.Value.RetainSignalsDays);
    }

    private string GetArgOrDefault(int index, string fallback)
    {
        return index >= 0 && index < _args.Length && !_args[index].StartsWith("--", StringComparison.Ordinal)
            ? _args[index]
            : fallback;
    }

    private static string ReportDedupeKey(Signal signal)
    {
        return $"{signal.Symbol}|{signal.Timeframe}|{signal.SignalType}";
    }

    private static string[] DistinctOrDefault(string[] values, string[] fallback)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinct.Length > 0 ? distinct : fallback;
    }
}
