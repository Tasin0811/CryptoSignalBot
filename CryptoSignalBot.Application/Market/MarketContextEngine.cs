using System.Globalization;
using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Market;

public sealed class MarketContextEngine : IMarketContextEngine
{
    public MarketContext Evaluate(
        IReadOnlyList<Candle> btcCandles,
        IReadOnlyList<Candle> benchmarkCandles,
        GlobalMarketData? globalMarketData = null)
    {
        var btcTrend = CalculateBtcTrend(btcCandles);
        var btcRiskOff = IsHardRiskOff(btcTrend);
        var globalRiskOff = IsGlobalRiskOff(globalMarketData);
        var btcContextPositive = IsPositiveBtcContext(btcTrend);
        var btcSoftPenalty = GetBtcSoftPenalty(btcTrend);
        var globalScoreImpact = GetGlobalScoreImpact(globalMarketData);

        if (btcRiskOff || globalRiskOff)
        {
            var reason = btcRiskOff
                ? "BTC risk-off"
                : "Global crypto market risk-off";

            return new MarketContext(true, false, -3m, $"{reason}: avoid new altcoin entries. {BuildGlobalSummary(globalMarketData)}".Trim());
        }

        var btcScoreImpact = btcContextPositive ? 1m : 0m;
        var scoreImpact = btcScoreImpact + btcSoftPenalty + globalScoreImpact;
        var summary = btcContextPositive
            ? "BTC trend context is supportive."
            : "BTC trend context is neutral or soft.";

        return new MarketContext(
            false,
            scoreImpact > 0m,
            Math.Clamp(scoreImpact, -2m, 2m),
            $"{summary} {BuildBtcSummary(btcTrend)} {BuildGlobalSummary(globalMarketData)}".Trim());
    }

    private static BtcTrendState CalculateBtcTrend(IReadOnlyList<Candle> candles)
    {
        if (candles.Count == 0)
        {
            return new BtcTrendState(null, null, null, null, null, false);
        }

        var closes = candles.Select(candle => candle.ClosePrice).ToArray();
        var latest = closes[^1];
        var oneCandleReturn = CalculateReturn(closes, 1);
        var threeCandleReturn = CalculateReturn(closes, 3);
        var ema50 = CalculateEma(closes, 50);
        var ema200 = CalculateEma(closes, 200);
        var emaBearish = latest > 0m && ema50.HasValue && ema200.HasValue && latest < ema200.Value && ema50.Value <= ema200.Value;

        return new BtcTrendState(latest, oneCandleReturn, threeCandleReturn, ema50, ema200, emaBearish);
    }

    private static bool IsHardRiskOff(BtcTrendState trend)
    {
        return trend.EmaBearish ||
               trend.OneCandleReturn <= -0.03m ||
               trend.ThreeCandleReturn <= -0.04m;
    }

    private static bool IsPositiveBtcContext(BtcTrendState trend)
    {
        if (trend.LatestClose is not > 0m)
        {
            return false;
        }

        if (trend.Ema50.HasValue && trend.Ema200.HasValue)
        {
            return trend.LatestClose > trend.Ema50.Value &&
                   trend.Ema50.Value > trend.Ema200.Value &&
                   trend.ThreeCandleReturn >= 0m;
        }

        return trend.OneCandleReturn > 0m;
    }

    private static decimal GetBtcSoftPenalty(BtcTrendState trend)
    {
        var penalty = 0m;
        if (trend.LatestClose is > 0m && trend.Ema50.HasValue && trend.LatestClose < trend.Ema50.Value)
        {
            penalty -= 1m;
        }

        if (trend.ThreeCandleReturn is <= -0.02m and > -0.04m)
        {
            penalty -= 1m;
        }

        return penalty;
    }

    private static bool IsGlobalRiskOff(GlobalMarketData? globalMarketData)
    {
        return globalMarketData?.MarketCapChangePercentage24hUsd <= -3m;
    }

    private static decimal GetGlobalScoreImpact(GlobalMarketData? globalMarketData)
    {
        if (globalMarketData?.MarketCapChangePercentage24hUsd is not { } marketCapChange)
        {
            return 0m;
        }

        var scoreImpact = marketCapChange switch
        {
            >= 1.5m => 1m,
            <= -1.5m => -1m,
            _ => 0m
        };

        if (globalMarketData.BitcoinDominancePercentage >= 58m)
        {
            scoreImpact -= 0.5m;
        }

        return scoreImpact;
    }

    private static decimal? CalculateReturn(IReadOnlyList<decimal> closes, int periods)
    {
        if (closes.Count <= periods)
        {
            return null;
        }

        var previous = closes[^(periods + 1)];
        var latest = closes[^1];
        return previous > 0m ? (latest - previous) / previous : null;
    }

    private static decimal? CalculateEma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period)
        {
            return null;
        }

        var multiplier = 2m / (period + 1);
        var ema = values.Take(period).Average();
        for (var index = period; index < values.Count; index++)
        {
            ema = ((values[index] - ema) * multiplier) + ema;
        }

        return ema;
    }

    private static string BuildBtcSummary(BtcTrendState trend)
    {
        var parts = new List<string>();
        if (trend.OneCandleReturn.HasValue)
        {
            parts.Add($"BTC last candle {FormatSignedPercentage(trend.OneCandleReturn.Value * 100m)}");
        }

        if (trend.ThreeCandleReturn.HasValue)
        {
            parts.Add($"BTC 3-candle {FormatSignedPercentage(trend.ThreeCandleReturn.Value * 100m)}");
        }

        if (trend.Ema50.HasValue && trend.Ema200.HasValue)
        {
            parts.Add(trend.EmaBearish ? "BTC below EMA200 with bearish EMA50/EMA200" : "BTC EMA trend usable");
        }

        return parts.Count == 0
            ? "BTC trend context unavailable."
            : string.Join(", ", parts) + ".";
    }

    private static string BuildGlobalSummary(GlobalMarketData? globalMarketData)
    {
        if (globalMarketData is null)
        {
            return "CoinGecko global context unavailable.";
        }

        var parts = new List<string>();
        if (globalMarketData.MarketCapChangePercentage24hUsd is { } marketCapChange)
        {
            parts.Add($"global market cap 24h {FormatSignedPercentage(marketCapChange)}");
        }

        if (globalMarketData.BitcoinDominancePercentage is { } btcDominance)
        {
            parts.Add($"BTC dominance {FormatPercentage(btcDominance)}");
        }

        return parts.Count == 0
            ? "CoinGecko global context had no usable values."
            : $"CoinGecko global context: {string.Join(", ", parts)}.";
    }

    private static string FormatSignedPercentage(decimal value)
    {
        return value > 0m
            ? $"+{FormatPercentage(value)}"
            : FormatPercentage(value);
    }

    private static string FormatPercentage(decimal value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value:0.##}%");
    }

    private sealed record BtcTrendState(
        decimal? LatestClose,
        decimal? OneCandleReturn,
        decimal? ThreeCandleReturn,
        decimal? Ema50,
        decimal? Ema200,
        bool EmaBearish);
}
