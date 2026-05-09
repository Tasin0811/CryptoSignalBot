using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Backtesting;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;

namespace CryptoSignalBot.Application.Backtesting;

public sealed class BacktestService(
    IPersistenceService persistenceService,
    IIndicatorEngine indicatorEngine,
    IMarketContextEngine marketContextEngine,
    IRiskEngine riskEngine,
    ISignalEngine signalEngine,
    IPaperTradingService paperTradingService) : IBacktestService
{
    public async Task<BacktestReport> RunAsync(BacktestOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var reports = new List<BacktestSymbolReport>();
        foreach (var timeframe in Distinct(options.Timeframes, normalizeUpper: false))
        {
            var btcCandles = await persistenceService.GetCandlesAsync("BTCUSDT", timeframe, options.MaxCandles, cancellationToken);

            foreach (var symbol in Distinct(options.Symbols, normalizeUpper: true))
            {
                var candles = await persistenceService.GetCandlesAsync(symbol, timeframe, options.MaxCandles, cancellationToken);
                reports.Add(RunSymbolBacktest(symbol, timeframe, candles, btcCandles, options));
            }
        }

        return new BacktestReport(DateTimeOffset.UtcNow, reports);
    }

    private BacktestSymbolReport RunSymbolBacktest(
        string symbol,
        string timeframe,
        IReadOnlyList<Candle> candles,
        IReadOnlyList<Candle> btcCandles,
        BacktestOptions options)
    {
        var ordered = candles.OrderBy(candle => candle.OpenTime).ToArray();
        var warmupCandles = Math.Max(2, options.WarmupCandles);
        var maxFutureCandles = Math.Max(1, options.MaxFutureCandles);
        var lastEntryIndex = ordered.Length - maxFutureCandles - 1;
        var evaluatedBars = 0;
        var results = new List<PaperTradeResult>();

        if (lastEntryIndex < warmupCandles - 1)
        {
            return new BacktestSymbolReport(symbol, timeframe, ordered.Length, 0, 0, results);
        }

        for (var entryIndex = warmupCandles - 1; entryIndex <= lastEntryIndex; entryIndex++)
        {
            var history = ordered.Take(entryIndex + 1).ToArray();
            var latest = history[^1];
            var future = ordered.Skip(entryIndex + 1).Take(maxFutureCandles).ToArray();
            evaluatedBars++;

            var indicators = indicatorEngine.Calculate(history);
            var atr = indicators.Atr14 ?? latest.ClosePrice * 0.02m;
            var riskPlan = riskEngine.CreatePlan(latest.ClosePrice, atr, options.AccountBalance, options.RiskPercent);
            var marketContext = marketContextEngine.Evaluate(GetBtcHistory(symbol, timeframe, latest.OpenTime, history, btcCandles), history);
            var signal = signalEngine.Analyze(symbol.ToUpperInvariant(), timeframe, latest.ClosePrice, indicators, marketContext, riskPlan)
                with { CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(latest.OpenTime, DateTimeKind.Utc)) };

            if (signal.Score < options.MinScore || !signal.StopLoss.HasValue || !signal.TakeProfit1.HasValue)
            {
                continue;
            }

            results.Add(paperTradingService.Evaluate(signal, future, maxFutureCandles));
        }

        return new BacktestSymbolReport(symbol, timeframe, ordered.Length, evaluatedBars, results.Count, results);
    }

    private static IReadOnlyList<Candle> GetBtcHistory(
        string symbol,
        string timeframe,
        DateTime entryTime,
        IReadOnlyList<Candle> fallback,
        IReadOnlyList<Candle> btcCandles)
    {
        if (string.Equals(symbol, "BTCUSDT", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        var history = btcCandles
            .Where(candle => string.Equals(candle.Timeframe, timeframe, StringComparison.OrdinalIgnoreCase) && candle.OpenTime <= entryTime)
            .OrderBy(candle => candle.OpenTime)
            .ToArray();

        return history.Length > 0 ? history : fallback;
    }

    private static IReadOnlyList<string> Distinct(IReadOnlyList<string> values, bool normalizeUpper)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Select(value => normalizeUpper ? value.ToUpperInvariant() : value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
