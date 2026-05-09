using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Application.Backtesting;
using CryptoSignalBot.Application.PaperTrading;
using CryptoSignalBot.Domain.Backtesting;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Indicators;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Tests;

public sealed class BacktestServiceTests
{
    [Fact]
    public async Task RunAsync_GeneratesSignalsFromSavedCandlesAndEvaluatesTrades()
    {
        var candles = new[]
        {
            CreateCandle(0, 100m, 101m, 99m, 100m),
            CreateCandle(1, 100m, 102m, 99m, 101m),
            CreateCandle(2, 101m, 107m, 100m, 106m)
        };
        var persistence = new FakePersistenceService(candles);
        var service = CreateService(persistence, score: 8m);
        var options = CreateOptions(minScore: 7.5m);

        var report = await service.RunAsync(options);

        Assert.Equal(1, report.TestedSetups);
        Assert.Equal(1, report.ClosedCount);
        Assert.Equal(1, report.Wins);
        Assert.Equal(100m, report.WinRate);
        Assert.Equal(PaperTradeOutcome.TakeProfit1, report.Results.Single().Outcome);
        Assert.Equal(new DateTime(2026, 5, 9, 1, 0, 0, DateTimeKind.Utc), report.Results.Single().CreatedAt);
    }

    [Fact]
    public async Task RunAsync_SkipsSignalsBelowMinimumScore()
    {
        var candles = new[]
        {
            CreateCandle(0, 100m, 101m, 99m, 100m),
            CreateCandle(1, 100m, 102m, 99m, 101m),
            CreateCandle(2, 101m, 107m, 100m, 106m)
        };
        var persistence = new FakePersistenceService(candles);
        var service = CreateService(persistence, score: 7.4m);
        var options = CreateOptions(minScore: 7.5m);

        var report = await service.RunAsync(options);

        Assert.Equal(0, report.TestedSetups);
        Assert.Empty(report.Results);
        Assert.Equal(1, report.Symbols.Single().EvaluatedBars);
    }

    private static BacktestService CreateService(FakePersistenceService persistence, decimal score)
    {
        return new BacktestService(
            persistence,
            new FakeIndicatorEngine(),
            new FakeMarketContextEngine(),
            new FakeRiskEngine(),
            new FakeSignalEngine(score),
            new PaperTradingService());
    }

    private static BacktestOptions CreateOptions(decimal minScore)
    {
        return new BacktestOptions(
            ["BTCUSDT"],
            ["1h"],
            MaxCandles: 10,
            WarmupCandles: 2,
            MaxFutureCandles: 1,
            MinScore: minScore,
            AccountBalance: 1000m,
            RiskPercent: 0.01m);
    }

    private static Candle CreateCandle(int hour, decimal open, decimal high, decimal low, decimal close)
    {
        var openTime = new DateTime(2026, 5, 9, hour, 0, 0, DateTimeKind.Utc);
        return new Candle("BTCUSDT", "1h", openTime, openTime.AddHours(1), open, high, low, close, 1000m);
    }

    private sealed class FakePersistenceService(IReadOnlyList<Candle> candles) : IPersistenceService
    {
        public Task SaveCandlesAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveSignalAsync(Signal signal, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Candle>> GetCandlesAsync(string symbol, string timeframe, int maxCandles, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Candle> result = candles
                .Where(candle => candle.Symbol == symbol && candle.Timeframe == timeframe)
                .OrderBy(candle => candle.OpenTime)
                .TakeLast(maxCandles)
                .ToArray();

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Signal>> GetSignalsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaperTradeReport> BuildPaperTradeReportAsync(int maxSignals, int maxFutureCandles, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CleanupDatabaseAsync(DateTimeOffset retainCandlesSince, DateTimeOffset retainSignalsSince, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeIndicatorEngine : IIndicatorEngine
    {
        public IndicatorSnapshot Calculate(IReadOnlyList<Candle> candles)
        {
            return new IndicatorSnapshot(null, null, null, null, null, null, 2m, null, null, null, null, null, null);
        }
    }

    private sealed class FakeMarketContextEngine : IMarketContextEngine
    {
        public MarketContext Evaluate(
            IReadOnlyList<Candle> btcCandles,
            IReadOnlyList<Candle> benchmarkCandles,
            GlobalMarketData? globalMarketData = null)
        {
            return new MarketContext(false, true, 1m, "test");
        }
    }

    private sealed class FakeRiskEngine : IRiskEngine
    {
        public RiskPlan CreatePlan(decimal entryPrice, decimal atr, decimal accountBalance, decimal riskPercent)
        {
            return new RiskPlan(RiskLevel.Low, entryPrice, entryPrice - 5m, entryPrice + 5m, entryPrice + 10m, 1.5m, 2.5m, 1m, "test");
        }
    }

    private sealed class FakeSignalEngine(decimal score) : ISignalEngine
    {
        public Signal Analyze(
            string symbol,
            string timeframe,
            decimal price,
            IndicatorSnapshot indicators,
            MarketContext marketContext,
            RiskPlan riskPlan)
        {
            return new Signal(
                symbol,
                timeframe,
                DateTimeOffset.UtcNow,
                price,
                score,
                SignalType.BuyWatch,
                RiskLevel.Low,
                price - 5m,
                price + 5m,
                price + 10m,
                1.5m,
                "test",
                Array.Empty<RuleResult>());
        }
    }
}
