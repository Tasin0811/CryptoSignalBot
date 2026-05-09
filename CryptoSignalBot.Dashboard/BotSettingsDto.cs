using CryptoSignalBot.Domain.Configuration;

namespace CryptoSignalBot.Dashboard;

public sealed record BotSettingsDto(
    string[] Symbols,
    string[] Timeframes,
    string[] ReportTimeframes,
    decimal MinScoreToNotify,
    int MaxReportSetups,
    int DeduplicateReportMinutes,
    int SignalDedupeMinutes,
    int RetainCandlesDays,
    int RetainSignalsDays,
    bool DryRunOnly,
    decimal AccountBalance,
    decimal RiskPercent)
{
    public static BotSettingsDto From(BotSettings settings)
    {
        return new BotSettingsDto(
            CleanSymbols(settings.Symbols, ["BTCUSDT"]),
            CleanTimeframes(settings.Timeframes, ["1h"]),
            CleanTimeframes(settings.ReportTimeframes, ["1h", "4h", "1d"]),
            settings.MinScoreToNotify,
            settings.MaxReportSetups,
            settings.DeduplicateReportMinutes,
            settings.SignalDedupeMinutes,
            settings.RetainCandlesDays,
            settings.RetainSignalsDays,
            settings.DryRunOnly,
            settings.AccountBalance,
            settings.RiskPercent);
    }

    public BotSettingsDto Sanitize()
    {
        return this with
        {
            Symbols = CleanSymbols(Symbols, ["BTCUSDT"]),
            Timeframes = CleanTimeframes(Timeframes, ["1h"]),
            ReportTimeframes = CleanTimeframes(ReportTimeframes, ["1h", "4h", "1d"]),
            MinScoreToNotify = Math.Clamp(MinScoreToNotify, 0m, 10m),
            MaxReportSetups = Math.Clamp(MaxReportSetups, 1, 50),
            DeduplicateReportMinutes = Math.Clamp(DeduplicateReportMinutes, 0, 10080),
            SignalDedupeMinutes = Math.Clamp(SignalDedupeMinutes, 0, 10080),
            RetainCandlesDays = Math.Clamp(RetainCandlesDays, 1, 3650),
            RetainSignalsDays = Math.Clamp(RetainSignalsDays, 1, 3650),
            AccountBalance = Math.Max(0m, AccountBalance),
            RiskPercent = Math.Clamp(RiskPercent, 0m, 0.20m)
        };
    }

    private static string[] CleanSymbols(string[] values, string[] fallback)
    {
        var cleaned = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length > 0 ? cleaned : fallback;
    }

    private static string[] CleanTimeframes(string[] values, string[] fallback)
    {
        var cleaned = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length > 0 ? cleaned : fallback;
    }
}
