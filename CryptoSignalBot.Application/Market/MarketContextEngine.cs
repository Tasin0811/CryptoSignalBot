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
        var btcRiskOff = IsRiskOff(btcCandles);
        var globalRiskOff = IsGlobalRiskOff(globalMarketData);
        var benchmarkPositive = IsPositive(benchmarkCandles);
        var globalScoreImpact = GetGlobalScoreImpact(globalMarketData);

        if (btcRiskOff || globalRiskOff)
        {
            var reason = btcRiskOff
                ? "BTC risk-off"
                : "Global crypto market risk-off";

            return new MarketContext(true, false, -3m, $"{reason}: avoid new altcoin entries. {BuildGlobalSummary(globalMarketData)}".Trim());
        }

        var candleScoreImpact = benchmarkPositive ? 1m : 0m;
        var scoreImpact = candleScoreImpact + globalScoreImpact;
        var summary = benchmarkPositive
            ? "Benchmark context is positive."
            : "Market context is neutral.";

        return new MarketContext(
            false,
            scoreImpact > 0m,
            scoreImpact,
            $"{summary} {BuildGlobalSummary(globalMarketData)}".Trim());
    }

    private static bool IsRiskOff(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 2)
        {
            return false;
        }

        var previous = candles[^2].ClosePrice;
        var latest = candles[^1].ClosePrice;
        return previous > 0 && (latest - previous) / previous <= -0.03m;
    }

    private static bool IsPositive(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 2)
        {
            return false;
        }

        return candles[^1].ClosePrice > candles[^2].ClosePrice;
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
}
