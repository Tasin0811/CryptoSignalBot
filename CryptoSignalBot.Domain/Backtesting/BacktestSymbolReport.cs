using CryptoSignalBot.Domain.PaperTrading;

namespace CryptoSignalBot.Domain.Backtesting;

public sealed record BacktestSymbolReport(
    string Symbol,
    string Timeframe,
    int CandleCount,
    int EvaluatedBars,
    int TestedSetups,
    IReadOnlyList<PaperTradeResult> Results)
{
    public int ClosedCount => Results.Count(result => result.Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.TakeProfit2 or PaperTradeOutcome.StopLoss);
    public int Wins => Results.Count(result => result.Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.TakeProfit2);
    public int Losses => Results.Count(result => result.Outcome == PaperTradeOutcome.StopLoss);
    public decimal WinRate => ClosedCount == 0 ? 0m : decimal.Round((decimal)Wins / ClosedCount * 100m, 2);
}
