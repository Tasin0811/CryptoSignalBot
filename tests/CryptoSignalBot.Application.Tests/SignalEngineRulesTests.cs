using CryptoSignalBot.Application.Signals;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Indicators;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Tests;

public sealed class SignalEngineRulesTests
{
    [Fact]
    public void Analyze_StrongSetup_ReturnsHighQualitySetupAndRuleBreakdown()
    {
        var engine = new SignalEngine();

        var signal = engine.Analyze(
            "ETHUSDT",
            "1h",
            110m,
            BullishIndicators(rsi14: 34m, volumeRatio: 1.8m),
            HealthyMarket(),
            ValidRisk());

        Assert.Equal(SignalType.HighQualitySetup, signal.SignalType);
        Assert.Equal(10m, signal.Score);
        Assert.NotEmpty(signal.RuleResults);
        Assert.Contains(signal.RuleResults, rule => rule.RuleName.Contains("Trend", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(signal.RuleResults, rule => rule.RuleName.Contains("Risk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_BtcRiskOff_ReturnsAvoid()
    {
        var engine = new SignalEngine();

        var signal = engine.Analyze(
            "ETHUSDT",
            "1h",
            110m,
            BullishIndicators(),
            new MarketContext(true, false, -3m, "BTC is dumping."),
            ValidRisk());

        Assert.Equal(SignalType.Avoid, signal.SignalType);
        Assert.Contains(signal.RuleResults, rule => rule.RuleName.Contains("BTC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_OverboughtRsiAndWeakVolume_PenalizesSetup()
    {
        var engine = new SignalEngine();

        var signal = engine.Analyze(
            "ETHUSDT",
            "1h",
            110m,
            BullishIndicators(rsi14: 76m, volumeRatio: 0.5m),
            new MarketContext(false, false, 0m, "Neutral context."),
            ValidRisk());

        Assert.NotEqual(SignalType.HighQualitySetup, signal.SignalType);
        Assert.Contains(signal.RuleResults, rule => rule.RuleName.Contains("RSI", StringComparison.OrdinalIgnoreCase) && rule.ScoreImpact < 0);
        Assert.Contains(signal.RuleResults, rule => rule.RuleName.Contains("volume", StringComparison.OrdinalIgnoreCase) && rule.ScoreImpact < 0);
    }

    [Fact]
    public void Analyze_NearSupport_AddsSmallPositiveSupportResistanceRule()
    {
        var engine = new SignalEngine();

        var signal = engine.Analyze(
            "ETHUSDT",
            "1h",
            110m,
            BullishIndicators(supportLevel: 108.9m, supportDistancePercent: 1m, resistanceLevel: 125m, resistanceDistancePercent: 13.64m),
            new MarketContext(false, false, 0m, "Neutral context."),
            ValidRisk());

        var rule = Assert.Single(signal.RuleResults, rule => rule.RuleName == "Support/resistance");
        Assert.Equal(0.75m, rule.ScoreImpact);
        Assert.Equal(RuleResultType.Pass, rule.Result);
        Assert.Contains("near recent support", rule.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("near recent support", signal.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_NearResistance_AddsWarningWithoutOverpoweringScore()
    {
        var engine = new SignalEngine();

        var signal = engine.Analyze(
            "ETHUSDT",
            "1h",
            110m,
            BullishIndicators(supportLevel: 100m, supportDistancePercent: 9.09m, resistanceLevel: 111.1m, resistanceDistancePercent: 1m),
            new MarketContext(false, false, 0m, "Neutral context."),
            ValidRisk());

        var rule = Assert.Single(signal.RuleResults, rule => rule.RuleName == "Support/resistance");
        Assert.Equal(-1.5m, rule.ScoreImpact);
        Assert.Equal(RuleResultType.Warning, rule.Result);
        Assert.Contains("near resistance", rule.Details, StringComparison.OrdinalIgnoreCase);
        Assert.True(signal.Score >= 6m);
    }

    private static IndicatorSnapshot BullishIndicators(
        decimal rsi14 = 45m,
        decimal volumeRatio = 1.2m,
        decimal? supportLevel = null,
        decimal? supportDistancePercent = null,
        decimal? resistanceLevel = null,
        decimal? resistanceDistancePercent = null)
    {
        return new IndicatorSnapshot(
            Ema50: 105m,
            Ema200: 100m,
            Rsi14: rsi14,
            Macd: 1.4m,
            MacdSignal: 1.0m,
            MacdHistogram: 0.40m,
            Atr14: 2m,
            BollingerUpper: 120m,
            BollingerMiddle: 110m,
            BollingerLower: 100m,
            Adx14: 30m,
            VolumeSma20: 1000m,
            VolumeRatio: volumeRatio,
            SupportLevel: supportLevel,
            ResistanceLevel: resistanceLevel,
            SupportDistancePercent: supportDistancePercent,
            ResistanceDistancePercent: resistanceDistancePercent);
    }

    private static MarketContext HealthyMarket() =>
        new(false, true, 1m, "Market context is positive.");

    private static RiskPlan ValidRisk() =>
        new(
            RiskLevel.Low,
            EntryPrice: 110m,
            StopLoss: 104m,
            TakeProfit1: 119m,
            TakeProfit2: 125m,
            RiskReward1: 1.5m,
            RiskReward2: 2.5m,
            PositionSize: 8.33333333m,
            Summary: "Valid risk plan.");
}
