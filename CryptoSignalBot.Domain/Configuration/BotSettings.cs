namespace CryptoSignalBot.Domain.Configuration;

public sealed class BotSettings
{
    public string[] Symbols { get; init; } = [];
    public string[] Timeframes { get; init; } = [];
    public string[] ReportTimeframes { get; init; } = ["1h", "4h", "1d"];
    public decimal MinScoreToNotify { get; init; } = 7.5m;
    public int MaxReportSetups { get; init; } = 8;
    public int DeduplicateReportMinutes { get; init; } = 60;
    public int SignalDedupeMinutes { get; init; } = 60;
    public int RetainCandlesDays { get; init; } = 365;
    public int RetainSignalsDays { get; init; } = 365;
    public bool DryRunOnly { get; init; } = true;
    public decimal AccountBalance { get; init; } = 1000m;
    public decimal RiskPercent { get; init; } = 0.01m;
    public decimal PaperPortfolioInitialBudget { get; init; } = 500m;
}
