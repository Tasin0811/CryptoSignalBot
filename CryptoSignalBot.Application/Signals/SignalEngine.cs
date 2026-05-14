using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Indicators;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Signals;

public sealed class SignalEngine : ISignalEngine
{
    public Signal Analyze(
        string symbol,
        string timeframe,
        decimal price,
        IndicatorSnapshot indicators,
        MarketContext marketContext,
        RiskPlan riskPlan)
    {
        var rules = new List<RuleResult>();

        AddTrendRules(price, indicators, rules);
        AddMomentumRules(indicators, rules);
        AddVolumeRules(indicators, rules);
        AddSupportResistanceRules(price, indicators, rules);
        AddMarketContextRules(marketContext, rules);
        AddRiskRules(riskPlan, rules);
        AddEntryQualityRules(indicators, marketContext, riskPlan, rules);

        var rawScore = rules.Sum(rule => rule.ScoreImpact);
        var normalizedScore = Math.Clamp(5m + rawScore, 0m, 10m);
        var signalType = ToSignalType(normalizedScore, rules);

        return new Signal(
            symbol,
            timeframe,
            DateTimeOffset.UtcNow,
            price,
            decimal.Round(normalizedScore, 2),
            signalType,
            riskPlan.RiskLevel,
            riskPlan.StopLoss,
            riskPlan.TakeProfit1,
            riskPlan.TakeProfit2,
            riskPlan.RiskReward1,
            BuildSummary(signalType, rules),
            rules);
    }

    private static void AddTrendRules(decimal price, IndicatorSnapshot indicators, ICollection<RuleResult> rules)
    {
        if (indicators.Ema50.HasValue && indicators.Ema200.HasValue)
        {
            if (price > indicators.Ema200 && indicators.Ema50 > indicators.Ema200)
            {
                rules.Add(new RuleResult("Trend EMA", 2m, RuleResultType.Pass, "Close above EMA200 and EMA50 above EMA200."));
            }
            else if (price < indicators.Ema200 && indicators.Ema50 < indicators.Ema200)
            {
                rules.Add(new RuleResult("Trend negative", -2m, RuleResultType.Fail, "Close below EMA200 and EMA50 below EMA200."));
            }
            else
            {
                rules.Add(new RuleResult("Trend mixed", -0.5m, RuleResultType.Neutral, "EMA trend is not aligned."));
            }
        }
    }

    private static void AddMomentumRules(IndicatorSnapshot indicators, ICollection<RuleResult> rules)
    {
        if (indicators.Rsi14 is >= 28m and <= 40m)
        {
            rules.Add(new RuleResult("RSI pullback", 1.5m, RuleResultType.Pass, "RSI is in a controlled pullback zone."));
        }
        else if (indicators.Rsi14 > 70m)
        {
            rules.Add(new RuleResult("RSI overbought", -1.5m, RuleResultType.Warning, "RSI is overbought."));
        }

        if (indicators.Macd.HasValue && indicators.MacdSignal.HasValue && indicators.MacdHistogram.HasValue)
        {
            var impact = indicators.Macd > indicators.MacdSignal && indicators.MacdHistogram > 0 ? 1m : -0.5m;
            rules.Add(new RuleResult("MACD momentum", impact, impact > 0 ? RuleResultType.Pass : RuleResultType.Neutral));
        }

        if (indicators.Adx14 > 25m)
        {
            rules.Add(new RuleResult("ADX trend strength", 1m, RuleResultType.Pass));
        }
        else if (indicators.Adx14 < 15m)
        {
            rules.Add(new RuleResult("ADX weak trend", -1m, RuleResultType.Warning));
        }
    }

    private static void AddVolumeRules(IndicatorSnapshot indicators, ICollection<RuleResult> rules)
    {
        if (indicators.VolumeRatio > 1.5m)
        {
            rules.Add(new RuleResult("Volume breakout", 2m, RuleResultType.Pass, "Volume is above 150% of recent average."));
        }
        else if (indicators.VolumeRatio > 1m)
        {
            rules.Add(new RuleResult("Volume confirmation", 1m, RuleResultType.Pass));
        }
        else if (indicators.VolumeRatio < 0.7m)
        {
            rules.Add(new RuleResult("Weak volume", -1m, RuleResultType.Warning));
        }
    }

    private static void AddSupportResistanceRules(decimal price, IndicatorSnapshot indicators, ICollection<RuleResult> rules)
    {
        if (!indicators.SupportLevel.HasValue && !indicators.ResistanceLevel.HasValue)
        {
            return;
        }

        var nearThresholdPercent = CalculateNearLevelThresholdPercent(price, indicators.Atr14);
        var nearSupport = indicators.SupportDistancePercent is >= 0m && indicators.SupportDistancePercent <= nearThresholdPercent;
        var nearResistance = indicators.ResistanceDistancePercent is >= 0m && indicators.ResistanceDistancePercent <= nearThresholdPercent;
        var details = FormatSupportResistanceDetails(indicators);

        if (nearSupport && nearResistance)
        {
            rules.Add(new RuleResult("Support/resistance", -0.25m, RuleResultType.Neutral, $"Price is compressed between nearby levels. {details}"));
        }
        else if (nearSupport)
        {
            rules.Add(new RuleResult("Support/resistance", 0.75m, RuleResultType.Pass, $"Price is near recent support. {details}"));
        }
        else if (nearResistance)
        {
            var impact = indicators.VolumeRatio >= 1.5m ? -0.5m : -1.5m;
            var message = indicators.VolumeRatio >= 1.5m
                ? "Price is near resistance, but volume is strong enough for a breakout watch."
                : "Price is near resistance without breakout-quality volume.";
            rules.Add(new RuleResult("Support/resistance", impact, RuleResultType.Warning, $"{message} {details}"));
        }
        else
        {
            rules.Add(new RuleResult("Support/resistance", 0m, RuleResultType.Neutral, details));
        }
    }

    private static void AddMarketContextRules(MarketContext marketContext, ICollection<RuleResult> rules)
    {
        if (marketContext.IsBtcRiskOff)
        {
            rules.Add(new RuleResult("BTC risk-off filter", -3m, RuleResultType.Blocked, marketContext.Summary));
        }
        else
        {
            rules.Add(new RuleResult("Market context", marketContext.ScoreImpact, marketContext.ScoreImpact > 0 ? RuleResultType.Pass : RuleResultType.Neutral, marketContext.Summary));
        }
    }

    private static void AddRiskRules(RiskPlan riskPlan, ICollection<RuleResult> rules)
    {
        if (riskPlan.RiskLevel == RiskLevel.Blocked)
        {
            rules.Add(new RuleResult("Risk plan blocked", -3m, RuleResultType.Blocked, riskPlan.Summary));
            return;
        }

        if (riskPlan.RiskReward1 >= 1.5m && riskPlan.RiskReward2 >= 2m)
        {
            rules.Add(new RuleResult("Risk reward", 1m, RuleResultType.Pass, riskPlan.Summary));
        }

        if (riskPlan.RiskLevel == RiskLevel.High)
        {
            rules.Add(new RuleResult("High risk", -1m, RuleResultType.Warning, riskPlan.Summary));
        }
    }

    private static void AddEntryQualityRules(
        IndicatorSnapshot indicators,
        MarketContext marketContext,
        RiskPlan riskPlan,
        ICollection<RuleResult> rules)
    {
        var issues = new List<string>();
        if (indicators.VolumeRatio < 0.8m)
        {
            issues.Add("volume below minimum confirmation");
        }

        if (indicators.Rsi14 > 65m)
        {
            issues.Add("RSI is too hot for a clean beginner entry");
        }

        if (indicators.Adx14.HasValue && indicators.Adx14 < 20m)
        {
            issues.Add("trend strength is weak");
        }

        if (marketContext.ScoreImpact < 0m)
        {
            issues.Add("market context is not supportive");
        }

        if (riskPlan.RiskReward1 < 1.5m || riskPlan.RiskReward2 < 2m)
        {
            issues.Add("risk/reward is not strong enough");
        }

        var resistanceWarning = rules.Any(rule =>
            string.Equals(rule.RuleName, "Support/resistance", StringComparison.OrdinalIgnoreCase) &&
            rule.Result == RuleResultType.Warning &&
            indicators.VolumeRatio < 1.5m);
        if (resistanceWarning)
        {
            issues.Add("resistance is too close without strong breakout volume");
        }

        if (issues.Count == 0)
        {
            rules.Add(new RuleResult("Entry quality gate", 0.5m, RuleResultType.Pass, "Entry quality checks passed."));
            return;
        }

        var impact = issues.Count >= 2 ? -2m : -1m;
        rules.Add(new RuleResult("Entry quality gate", impact, RuleResultType.Warning, string.Join("; ", issues) + "."));
    }

    private static SignalType ToSignalType(decimal score, IReadOnlyCollection<RuleResult> rules)
    {
        if (rules.Any(rule => rule.Result == RuleResultType.Blocked))
        {
            return SignalType.Avoid;
        }

        var entryQualityIssue = rules.FirstOrDefault(rule =>
            string.Equals(rule.RuleName, "Entry quality gate", StringComparison.OrdinalIgnoreCase) &&
            rule.Result == RuleResultType.Warning);

        var signalType = score switch
        {
            < 4m => SignalType.Avoid,
            < 6m => SignalType.Wait,
            < 7.5m => SignalType.Watch,
            < 8.5m => SignalType.BuyWatch,
            _ => SignalType.HighQualitySetup
        };

        if (entryQualityIssue is null)
        {
            return signalType;
        }

        if (entryQualityIssue.ScoreImpact <= -2m && signalType >= SignalType.BuyWatch)
        {
            return SignalType.Watch;
        }

        return signalType == SignalType.HighQualitySetup
            ? SignalType.BuyWatch
            : signalType;
    }

    private static string BuildSummary(SignalType signalType, IReadOnlyCollection<RuleResult> rules)
    {
        var positives = rules.Count(rule => rule.ScoreImpact > 0);
        var negatives = rules.Count(rule => rule.ScoreImpact < 0);
        var supportResistance = rules.FirstOrDefault(rule =>
            string.Equals(rule.RuleName, "Support/resistance", StringComparison.OrdinalIgnoreCase) && rule.ScoreImpact != 0m);
        var levelSummary = supportResistance is null ? string.Empty : $" {supportResistance.Details}";

        return $"{signalType}: {positives} positive rules, {negatives} negative rules.{levelSummary}";
    }

    private static decimal CalculateNearLevelThresholdPercent(decimal price, decimal? atr)
    {
        if (price <= 0m || atr is not > 0m)
        {
            return 1m;
        }

        var atrPercent = atr.Value / price * 100m;
        return Math.Clamp(atrPercent * 1.5m, 1m, 3m);
    }

    private static string FormatSupportResistanceDetails(IndicatorSnapshot indicators)
    {
        var support = FormatLevel("support", indicators.SupportLevel, indicators.SupportDistancePercent);
        var resistance = FormatLevel("resistance", indicators.ResistanceLevel, indicators.ResistanceDistancePercent);

        return string.Join("; ", new[] { support, resistance }.Where(value => value.Length > 0));
    }

    private static string FormatLevel(string label, decimal? level, decimal? distancePercent)
    {
        if (!level.HasValue || !distancePercent.HasValue)
        {
            return string.Empty;
        }

        return $"{label} {level.Value:0.########} ({distancePercent.Value:0.##}%)";
    }
}
