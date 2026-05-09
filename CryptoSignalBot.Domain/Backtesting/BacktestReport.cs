using CryptoSignalBot.Domain.PaperTrading;

namespace CryptoSignalBot.Domain.Backtesting;

public sealed record BacktestReport(
    DateTimeOffset CreatedAt,
    IReadOnlyList<BacktestSymbolReport> Symbols)
{
    public int TestedSetups => Symbols.Sum(symbol => symbol.TestedSetups);
    public int ClosedCount => Results.Count(result => result.Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.StopLoss);
    public int Wins => Results.Count(result => result.Outcome == PaperTradeOutcome.TakeProfit1);
    public int Losses => Results.Count(result => result.Outcome == PaperTradeOutcome.StopLoss);
    public decimal WinRate => ClosedCount == 0 ? 0m : decimal.Round((decimal)Wins / ClosedCount * 100m, 2);
    public decimal AverageReturnPercent => ClosedCount == 0
        ? 0m
        : decimal.Round(Results.Where(result => result.ReturnPercent.HasValue).Average(result => result.ReturnPercent!.Value), 4);

    public IReadOnlyList<PaperTradeResult> Results => Symbols.SelectMany(symbol => symbol.Results).ToArray();
}
