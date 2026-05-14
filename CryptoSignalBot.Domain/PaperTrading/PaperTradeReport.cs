namespace CryptoSignalBot.Domain.PaperTrading;

public sealed record PaperTradeReport(DateTimeOffset CreatedAt, IReadOnlyList<PaperTradeResult> Results)
{
    public int ClosedCount => Results.Count(result => result.Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.TakeProfit2 or PaperTradeOutcome.StopLoss);
    public int Wins => Results.Count(result => result.Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.TakeProfit2);
    public int Losses => Results.Count(result => result.Outcome == PaperTradeOutcome.StopLoss);
    public int OpenCount => Results.Count(result => result.Outcome == PaperTradeOutcome.Open);
    public int ExpiredCount => Results.Count(result => result.Outcome == PaperTradeOutcome.Expired);
    public int InvalidCount => Results.Count(result => result.Outcome == PaperTradeOutcome.Invalid);
    public decimal WinRate => ClosedCount == 0 ? 0m : decimal.Round((decimal)Wins / ClosedCount * 100m, 2);

    public IReadOnlyList<PaperTradeResult> RecentClosedTrades(int count)
    {
        return Results
            .Where(result => result.Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.TakeProfit2 or PaperTradeOutcome.StopLoss)
            .OrderByDescending(result => result.ExitTime ?? result.CreatedAt)
            .ThenByDescending(result => result.CreatedAt)
            .Take(count)
            .ToArray();
    }
}
