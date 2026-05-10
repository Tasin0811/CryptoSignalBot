namespace CryptoSignalBot.Domain.PaperTrading;

public sealed record PaperPortfolioReport(
    DateTimeOffset CreatedAt,
    decimal InitialBudget,
    decimal Cash,
    decimal OpenPositionValue,
    IReadOnlyList<PaperPortfolioTrade> Trades)
{
    public decimal Equity => Cash + OpenPositionValue;
    public decimal ProfitLoss => Equity - InitialBudget;
    public decimal ProfitLossPercent => InitialBudget <= 0m ? 0m : decimal.Round(ProfitLoss / InitialBudget * 100m, 2);
    public int ClosedCount => Trades.Count(trade => trade.IsClosed);
    public int OpenCount => Trades.Count(trade => !trade.IsClosed);
    public int Wins => Trades.Count(trade => trade.IsClosed && trade.ProfitLoss > 0m);
    public int Losses => Trades.Count(trade => trade.IsClosed && trade.ProfitLoss < 0m);
    public decimal WinRate => ClosedCount == 0 ? 0m : decimal.Round((decimal)Wins / ClosedCount * 100m, 2);
}

public sealed record PaperPortfolioTrade(
    long SignalId,
    string Symbol,
    string Timeframe,
    DateTime EntryTime,
    decimal EntryPrice,
    decimal Units,
    decimal Invested,
    DateTime? ExitTime,
    decimal? ExitPrice,
    decimal CurrentPrice,
    PaperTradeOutcome Outcome)
{
    public bool IsClosed => Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.StopLoss or PaperTradeOutcome.Expired;
    public decimal CurrentValue => Units * (IsClosed ? ExitPrice ?? CurrentPrice : CurrentPrice);
    public decimal ProfitLoss => CurrentValue - Invested;
    public decimal ProfitLossPercent => Invested <= 0m ? 0m : decimal.Round(ProfitLoss / Invested * 100m, 2);
}
