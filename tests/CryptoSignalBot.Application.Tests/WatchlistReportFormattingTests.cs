using System.Net;
using System.Text.Json;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.Signals;
using CryptoSignalBot.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Application.Tests;

public sealed class WatchlistReportFormattingTests
{
    [Fact]
    public async Task TelegramReport_UsesCompactTopSignalsAboveThreshold()
    {
        var handler = new CapturingHandler();
        var notifier = new TelegramNotifier(
            new HttpClient(handler),
            Options.Create(new TelegramSettings
            {
                BotToken = "test-token",
                ChatId = "test-chat"
            }));

        var report = new WatchlistReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            MinScoreToNotify: 7m,
            Enumerable.Range(1, 22)
                .Select(index => CreateSignal(
                    $"ALT{index:00}USDT",
                    "1h",
                    30_000m + index,
                    score: 10m - (index * 0.1m)))
                .Append(CreateSignal("BELOWUSDT", "4h", 99m, score: 6.9m))
                .ToArray(),
            AnalyzedCount: 23,
            MaxSetups: 8,
            SuppressedDuplicates: 3);

        await notifier.SendReportAsync(report);

        var text = handler.ReadJsonPayload().GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Equal(23, report.AnalyzedCount);
        Assert.Contains("*CryptoSignalBot Report*", text);
        Assert.Contains("2026-05-09 14:30 UTC", text);
        Assert.Contains("Top setups: 8", text);
        Assert.Contains("Suppressed duplicates: 3", text);
        Assert.Contains("ALT01USDT 1h - Da osservare per possibile acquisto 9.9/10", text);
        Assert.Contains("ALT08USDT 1h - Da osservare per possibile acquisto 9.2/10", text);
        Assert.Contains("Setup interessante: osservare", text);
        Assert.DoesNotContain("ALT09USDT", text);
        Assert.DoesNotContain("BELOWUSDT", text);
        Assert.DoesNotContain("Price", text);
        Assert.DoesNotContain("SL", text);
        Assert.Equal(8, text.Split('\n').Count(line => line.Contains("USDT ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TelegramReport_WhenNoSignalsMeetThreshold_UsesCompactEmptyMessage()
    {
        var handler = new CapturingHandler();
        var notifier = new TelegramNotifier(
            new HttpClient(handler),
            Options.Create(new TelegramSettings
            {
                BotToken = "test-token",
                ChatId = "test-chat"
            }));

        var report = new WatchlistReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            MinScoreToNotify: 7m,
            new[] { CreateSignal("ETHUSDT", "1h", 2_500m, score: 6.5m) },
            AnalyzedCount: 1);

        await notifier.SendReportAsync(report);

        var text = handler.ReadJsonPayload().GetProperty("text").GetString();

        Assert.Equal(1, report.AnalyzedCount);
        Assert.Equal(
            "*CryptoSignalBot Report*\n2026-05-09 14:30 UTC\nNo signals above threshold 7/10.",
            text);
    }

    [Fact]
    public async Task TelegramReport_WhenWatchlistIsEmpty_UsesCompactEmptyMessage()
    {
        var handler = new CapturingHandler();
        var notifier = new TelegramNotifier(
            new HttpClient(handler),
            Options.Create(new TelegramSettings
            {
                BotToken = "test-token",
                ChatId = "test-chat"
            }));

        var report = new WatchlistReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            MinScoreToNotify: 7m,
            Array.Empty<Signal>(),
            AnalyzedCount: 0);

        await notifier.SendReportAsync(report);

        var text = handler.ReadJsonPayload().GetProperty("text").GetString();

        Assert.Equal(0, report.AnalyzedCount);
        Assert.Empty(report.Signals);
        Assert.Empty(report.NotifiableSignals);
        Assert.Equal(
            "*CryptoSignalBot Report*\n2026-05-09 14:30 UTC\nNo signals above threshold 7/10.",
            text);
    }

    [Fact]
    public void WatchlistReport_NotifiableSignalsPreservesAnalyzedSignalCount()
    {
        var signals = new[]
        {
            CreateSignal("ETHUSDT", "1h", 2_500m, score: 6.5m),
            CreateSignal("BTCUSDT", "4h", 65_000m, score: 8m),
            CreateSignal("SOLUSDT", "1h", 150m, score: 7.5m)
        };

        var report = new WatchlistReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            MinScoreToNotify: 7m,
            signals,
            AnalyzedCount: 3);

        Assert.Equal(3, report.AnalyzedCount);
        Assert.Same(signals, report.Signals);
        Assert.Collection(
            report.NotifiableSignals,
            signal => Assert.Equal("BTCUSDT", signal.Symbol),
            signal => Assert.Equal("SOLUSDT", signal.Symbol));
    }

    [Fact]
    public async Task TelegramPaperTradeReport_SendsCompactSummary()
    {
        var handler = new CapturingHandler();
        var notifier = new TelegramNotifier(
            new HttpClient(handler),
            Options.Create(new TelegramSettings
            {
                BotToken = "test-token",
                ChatId = "test-chat"
            }));

        var report = new PaperTradeReport(
            new DateTimeOffset(2026, 5, 9, 14, 30, 0, TimeSpan.Zero),
            new[]
            {
                new PaperTradeResult(
                    1,
                    "BTCUSDT",
                    "1h",
                    new DateTime(2026, 5, 9, 10, 0, 0),
                    80_000m,
                    79_000m,
                    82_000m,
                    8m,
                    "BuyWatch",
                    PaperTradeOutcome.TakeProfit1,
                    new DateTime(2026, 5, 9, 12, 0, 0),
                    82_000m,
                    2.5m),
                new PaperTradeResult(
                    2,
                    "ETHUSDT",
                    "4h",
                    new DateTime(2026, 5, 9, 11, 0, 0),
                    2_300m,
                    2_250m,
                    2_400m,
                    7.5m,
                    "BuyWatch",
                    PaperTradeOutcome.Open,
                    null,
                    null,
                    null)
            });

        await notifier.SendPaperTradeReportAsync(report);

        var text = handler.ReadJsonPayload().GetProperty("text").GetString();

        Assert.NotNull(text);
        Assert.Contains("*CryptoSignalBot Paper Trading*", text);
        Assert.Contains("Signals: 2 | Closed: 1 | Open: 1", text);
        Assert.Contains("Wins: 1 | Losses: 0 | Win rate: 100%", text);
        Assert.Contains("BTCUSDT 1h WIN +2.5% score 8", text);
        Assert.DoesNotContain("ETHUSDT", text);
    }

    private static Signal CreateSignal(string symbol, string timeframe, decimal price, decimal score)
    {
        return new Signal(
            symbol,
            timeframe,
            new DateTimeOffset(2026, 5, 9, 14, 0, 0, TimeSpan.Zero),
            price,
            score,
            SignalType.BuyWatch,
            RiskLevel.Low,
            StopLoss: price * 0.95m,
            TakeProfit1: price * 1.05m,
            TakeProfit2: price * 1.10m,
            RiskReward: 2m,
            Summary: "Compact report test signal.",
            RuleResults: Array.Empty<RuleResult>());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private string? _content;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        public JsonElement ReadJsonPayload()
        {
            Assert.False(string.IsNullOrWhiteSpace(_content));
            return JsonDocument.Parse(_content).RootElement.Clone();
        }
    }
}
