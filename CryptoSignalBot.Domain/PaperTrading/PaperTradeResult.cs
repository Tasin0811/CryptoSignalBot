namespace CryptoSignalBot.Domain.PaperTrading;

public sealed record PaperTradeResult(
    long SignalId,
    string Symbol,
    string Timeframe,
    DateTime CreatedAt,
    decimal EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit1,
    decimal Score,
    string SignalType,
    PaperTradeOutcome Outcome,
    DateTime? ExitTime,
    decimal? ExitPrice,
    decimal? ReturnPercent);
