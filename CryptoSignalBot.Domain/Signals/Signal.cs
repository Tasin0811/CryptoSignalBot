using CryptoSignalBot.Domain.Enums;

namespace CryptoSignalBot.Domain.Signals;

public sealed record Signal(
    string Symbol,
    string Timeframe,
    DateTimeOffset CreatedAt,
    decimal Price,
    decimal Score,
    SignalType SignalType,
    RiskLevel RiskLevel,
    decimal? StopLoss,
    decimal? TakeProfit1,
    decimal? TakeProfit2,
    decimal? RiskReward,
    string Summary,
    IReadOnlyList<RuleResult> RuleResults);
