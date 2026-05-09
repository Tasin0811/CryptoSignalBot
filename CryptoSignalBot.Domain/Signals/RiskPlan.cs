using CryptoSignalBot.Domain.Enums;

namespace CryptoSignalBot.Domain.Signals;

public sealed record RiskPlan(
    RiskLevel RiskLevel,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit1,
    decimal? TakeProfit2,
    decimal? RiskReward1,
    decimal? RiskReward2,
    decimal? PositionSize,
    string Summary);
