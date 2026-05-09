namespace CryptoSignalBot.Domain.Backtesting;

public sealed record BacktestOptions(
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> Timeframes,
    int MaxCandles,
    int WarmupCandles,
    int MaxFutureCandles,
    decimal MinScore,
    decimal AccountBalance,
    decimal RiskPercent);
