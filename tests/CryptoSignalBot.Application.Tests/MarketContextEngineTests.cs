using CryptoSignalBot.Application.Market;
using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Tests;

public sealed class MarketContextEngineTests
{
    [Fact]
    public void Evaluate_WhenGlobalContextUnavailable_UsesCandleOnlyContext()
    {
        var engine = new MarketContextEngine();

        var context = engine.Evaluate(FlatCandles(), RisingCandles(), globalMarketData: null);

        Assert.False(context.IsBtcRiskOff);
        Assert.False(context.IsAltcoinContextPositive);
        Assert.Equal(0m, context.ScoreImpact);
        Assert.Contains("unavailable", context.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenGlobalMarketIsPositive_AddsScoreImpact()
    {
        var engine = new MarketContextEngine();

        var context = engine.Evaluate(
            BullishBtcTrendCandles(),
            RisingCandles(),
            GlobalMarket(marketCapChangePercentage24hUsd: 2.2m));

        Assert.False(context.IsBtcRiskOff);
        Assert.True(context.IsAltcoinContextPositive);
        Assert.Equal(2m, context.ScoreImpact);
        Assert.Contains("+2.2%", context.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenGlobalMarketIsRiskOff_BlocksAltcoinEntries()
    {
        var engine = new MarketContextEngine();

        var context = engine.Evaluate(
            FlatCandles(),
            RisingCandles(),
            GlobalMarket(marketCapChangePercentage24hUsd: -3.4m));

        Assert.True(context.IsBtcRiskOff);
        Assert.False(context.IsAltcoinContextPositive);
        Assert.Equal(-3m, context.ScoreImpact);
        Assert.Contains("Global crypto market risk-off", context.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenBitcoinDominanceIsHigh_PenalizesAltcoinContext()
    {
        var engine = new MarketContextEngine();

        var context = engine.Evaluate(
            BullishBtcTrendCandles(),
            RisingCandles(),
            GlobalMarket(marketCapChangePercentage24hUsd: 0.5m, bitcoinDominancePercentage: 59m));

        Assert.False(context.IsBtcRiskOff);
        Assert.True(context.IsAltcoinContextPositive);
        Assert.Equal(0.5m, context.ScoreImpact);
        Assert.Contains("BTC dominance 59%", context.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WhenBtcDropsThreeCandles_PutsRiskOff()
    {
        var engine = new MarketContextEngine();

        var context = engine.Evaluate(
            [
                CandleAt(100m),
                CandleAt(99m),
                CandleAt(97m),
                CandleAt(95m)
            ],
            RisingCandles(),
            globalMarketData: null);

        Assert.True(context.IsBtcRiskOff);
        Assert.False(context.IsAltcoinContextPositive);
        Assert.Equal(-3m, context.ScoreImpact);
    }

    private static IReadOnlyList<Candle> FlatCandles() =>
    [
        CandleAt(100m),
        CandleAt(100m)
    ];

    private static IReadOnlyList<Candle> RisingCandles() =>
    [
        CandleAt(100m),
        CandleAt(102m)
    ];

    private static IReadOnlyList<Candle> BullishBtcTrendCandles() =>
        Enumerable.Range(0, 220)
            .Select(index => CandleAt(100m + index))
            .ToArray();

    private static Candle CandleAt(decimal closePrice) =>
        new("ETHUSDT", "1h", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 99m, 103m, 98m, closePrice, 1000m);

    private static GlobalMarketData GlobalMarket(
        decimal marketCapChangePercentage24hUsd,
        decimal bitcoinDominancePercentage = 50m) =>
        new(
            TotalMarketCapUsd: 2_500_000_000_000m,
            TotalVolumeUsd: 90_000_000_000m,
            MarketCapChangePercentage24hUsd: marketCapChangePercentage24hUsd,
            BitcoinDominancePercentage: bitcoinDominancePercentage,
            EthereumDominancePercentage: 17m,
            ActiveCryptocurrencies: 12_000,
            UpdatedAt: DateTimeOffset.UtcNow);
}
