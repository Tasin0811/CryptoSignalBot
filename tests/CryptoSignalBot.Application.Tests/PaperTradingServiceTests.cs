using CryptoSignalBot.Application.PaperTrading;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;
using CryptoSignalBot.Infrastructure.Notifications;

namespace CryptoSignalBot.Application.Tests;

public sealed class PaperTradingServiceTests
{
    [Fact]
    public void Evaluate_ClosesPartialAtTakeProfit1ThenMovesRemainingStopToBreakEven()
    {
        var service = new PaperTradingService();
        var signal = CreateSignal();
        var candles = new[]
        {
            CreateFutureCandle(high: 106m, low: 101m, close: 105m),
            CreateFutureCandle(openOffsetHours: 2, high: 104m, low: 100m, close: 101m)
        };

        var result = service.Evaluate(signal, candles, maxCandles: 10);

        Assert.Equal(PaperTradeOutcome.TakeProfit1, result.Outcome);
        Assert.Equal(100m, result.ExitPrice);
        Assert.Equal(0m, result.ReturnPercent);
    }

    [Fact]
    public void Evaluate_ClosesPartialAtTakeProfit1ThenRemainingAtTakeProfit2()
    {
        var service = new PaperTradingService();
        var signal = CreateSignal();
        var candles = new[]
        {
            CreateFutureCandle(high: 106m, low: 101m, close: 105m),
            CreateFutureCandle(openOffsetHours: 2, high: 111m, low: 104m, close: 110m)
        };

        var result = service.Evaluate(signal, candles, maxCandles: 10);

        Assert.Equal(PaperTradeOutcome.TakeProfit2, result.Outcome);
        Assert.Equal(110m, result.ExitPrice);
        Assert.Equal(10m, result.ReturnPercent);
    }

    [Fact]
    public void Evaluate_ClosesAtStopLossBeforeTakeProfitWhenBothHitSameCandle()
    {
        var service = new PaperTradingService();
        var signal = CreateSignal();
        var candles = new[] { CreateFutureCandle(high: 106m, low: 94m, close: 100m) };

        var result = service.Evaluate(signal, candles, maxCandles: 10);

        Assert.Equal(PaperTradeOutcome.StopLoss, result.Outcome);
        Assert.Equal(95m, result.ExitPrice);
        Assert.Equal(-5m, result.ReturnPercent);
    }

    [Fact]
    public void Evaluate_ReturnsOpenWhenNotEnoughFutureCandles()
    {
        var service = new PaperTradingService();
        var signal = CreateSignal();
        var candles = new[] { CreateFutureCandle(high: 102m, low: 98m, close: 101m) };

        var result = service.Evaluate(signal, candles, maxCandles: 10);

        Assert.Equal(PaperTradeOutcome.Open, result.Outcome);
        Assert.Null(result.ExitPrice);
    }

    [Fact]
    public void Evaluate_ReturnsInvalidWithoutRiskPlan()
    {
        var service = new PaperTradingService();
        var signal = CreateSignal(stopLoss: null, takeProfit1: null);

        var result = service.Evaluate(signal, Array.Empty<Candle>(), maxCandles: 10);

        Assert.Equal(PaperTradeOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void PaperTradeReport_SummarizesOutcomesAndRecentClosedTrades()
    {
        var report = new PaperTradeReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            [
                CreateResult("BTCUSDT", PaperTradeOutcome.TakeProfit1, new DateTime(2026, 5, 9, 16, 0, 0, DateTimeKind.Utc), 5m),
                CreateResult("ETHUSDT", PaperTradeOutcome.StopLoss, new DateTime(2026, 5, 9, 15, 0, 0, DateTimeKind.Utc), -5m),
                CreateResult("SOLUSDT", PaperTradeOutcome.Open, null, null),
                CreateResult("XRPUSDT", PaperTradeOutcome.Expired, new DateTime(2026, 5, 9, 13, 0, 0, DateTimeKind.Utc), 1m)
            ]);

        Assert.Equal(2, report.ClosedCount);
        Assert.Equal(1, report.Wins);
        Assert.Equal(1, report.Losses);
        Assert.Equal(1, report.OpenCount);
        Assert.Equal(1, report.ExpiredCount);
        Assert.Equal(50m, report.WinRate);
        Assert.Collection(
            report.RecentClosedTrades(2),
            trade => Assert.Equal("BTCUSDT", trade.Symbol),
            trade => Assert.Equal("ETHUSDT", trade.Symbol));
    }

    [Fact]
    public void PaperTradeReportFormatter_IncludesSummaryAndRecentClosedTrades()
    {
        var report = new PaperTradeReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            [
                CreateResult("BTCUSDT", PaperTradeOutcome.TakeProfit1, new DateTime(2026, 5, 9, 16, 0, 0, DateTimeKind.Utc), 5m),
                CreateResult("ETHUSDT", PaperTradeOutcome.StopLoss, new DateTime(2026, 5, 9, 15, 0, 0, DateTimeKind.Utc), -5m),
                CreateResult("SOLUSDT", PaperTradeOutcome.Open, null, null),
                CreateResult("ADAUSDT", PaperTradeOutcome.Invalid, null, null),
                CreateResult("XRPUSDT", PaperTradeOutcome.Expired, new DateTime(2026, 5, 9, 13, 0, 0, DateTimeKind.Utc), 1m)
            ]);

        var subject = PaperTradeReportFormatter.FormatSubject(report);
        var text = PaperTradeReportFormatter.FormatText(report, recentClosedCount: 1);

        Assert.Equal("CryptoSignalBot Paper Trading - 1W/1L - 2026-05-09 14:30", subject);
        Assert.Contains("CryptoSignalBot Paper Trading Report", text);
        Assert.Contains("Evaluated signals: 5", text);
        Assert.Contains("Wins: 1", text);
        Assert.Contains("Losses: 1", text);
        Assert.Contains("Open: 1", text);
        Assert.Contains("Expired: 1", text);
        Assert.Contains("Invalid: 1", text);
        Assert.Contains("Win rate: 50%", text);
        Assert.Contains("2026-05-09 16:00 BTCUSDT 1h BuyWatch score 8: WIN entry 100 exit 105 return +5%", text);
        Assert.DoesNotContain("ETHUSDT", text);
    }

    private static Signal CreateSignal(decimal? stopLoss = 95m, decimal? takeProfit1 = 105m)
    {
        return new Signal(
            "BTCUSDT",
            "1h",
            new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero),
            100m,
            8m,
            SignalType.BuyWatch,
            RiskLevel.Low,
            stopLoss,
            takeProfit1,
            110m,
            1.5m,
            "paper test",
            Array.Empty<RuleResult>());
    }

    private static Candle CreateFutureCandle(decimal high, decimal low, decimal close, int openOffsetHours = 1)
    {
        return new Candle(
            "BTCUSDT",
            "1h",
            new DateTime(2026, 5, 9, 12 + openOffsetHours, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 9, 12 + openOffsetHours, 59, 59, DateTimeKind.Utc),
            100m,
            high,
            low,
            close,
            1000m);
    }

    private static PaperTradeResult CreateResult(
        string symbol,
        PaperTradeOutcome outcome,
        DateTime? exitTime,
        decimal? returnPercent)
    {
        return new PaperTradeResult(
            SignalId: 1,
            Symbol: symbol,
            Timeframe: "1h",
            CreatedAt: new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
            EntryPrice: 100m,
            StopLoss: 95m,
            TakeProfit1: 105m,
            Score: 8m,
            SignalType: SignalType.BuyWatch.ToString(),
            Outcome: outcome,
            ExitTime: exitTime,
            ExitPrice: returnPercent switch
            {
                5m => 105m,
                -5m => 95m,
                1m => 101m,
                _ => null
            },
            ReturnPercent: returnPercent);
    }
}
