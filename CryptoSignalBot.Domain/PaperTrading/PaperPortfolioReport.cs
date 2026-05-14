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
    public DateTime? FirstTradeAt => Trades.Count == 0 ? null : Trades.Min(trade => trade.EntryTime);
    public DateTime? LastTradeAt => Trades.Count == 0 ? null : Trades.Max(trade => trade.ExitTime ?? trade.EntryTime);
    public decimal TotalInvested => Trades.Sum(trade => trade.Invested);
    public decimal RealizedProfitLoss => Trades.Where(trade => trade.IsClosed).Sum(trade => trade.ProfitLoss);
    public decimal UnrealizedProfitLoss => Trades.Where(trade => !trade.IsClosed).Sum(trade => trade.ProfitLoss);
    public decimal TotalFees => Trades.Sum(trade => trade.TotalFees);
    public decimal CapitalAtWorkPercent => Equity <= 0m ? 0m : decimal.Round(OpenPositionValue / Equity * 100m, 2);
    public decimal AverageInvested => Trades.Count == 0 ? 0m : decimal.Round(TotalInvested / Trades.Count, 8);
    public decimal BestTradeProfitLoss => Trades.Count == 0 ? 0m : Trades.Max(trade => trade.ProfitLoss);
    public decimal WorstTradeProfitLoss => Trades.Count == 0 ? 0m : Trades.Min(trade => trade.ProfitLoss);
    public decimal AverageWin => Wins == 0 ? 0m : decimal.Round(Trades.Where(trade => trade.IsClosed && trade.ProfitLoss > 0m).Average(trade => trade.ProfitLoss), 8);
    public decimal AverageLoss => Losses == 0 ? 0m : decimal.Round(Trades.Where(trade => trade.IsClosed && trade.ProfitLoss < 0m).Average(trade => trade.ProfitLoss), 8);
    public decimal ProfitFactor
    {
        get
        {
            var grossProfit = Trades.Where(trade => trade.IsClosed && trade.ProfitLoss > 0m).Sum(trade => trade.ProfitLoss);
            var grossLoss = Math.Abs(Trades.Where(trade => trade.IsClosed && trade.ProfitLoss < 0m).Sum(trade => trade.ProfitLoss));
            return grossLoss <= 0m ? (grossProfit > 0m ? decimal.Round(grossProfit, 8) : 0m) : decimal.Round(grossProfit / grossLoss, 4);
        }
    }
    public decimal Expectancy => ClosedCount == 0 ? 0m : decimal.Round(Trades.Where(trade => trade.IsClosed).Average(trade => trade.ProfitLoss), 8);
    public decimal ExpectancyPercent => InitialBudget <= 0m ? 0m : decimal.Round(Expectancy / InitialBudget * 100m, 4);
    public decimal MaxDrawdown => CalculateMaxDrawdown().Amount;
    public decimal MaxDrawdownPercent => CalculateMaxDrawdown().Percent;
    public int ClosedCount => Trades.Count(trade => trade.IsClosed);
    public int OpenCount => Trades.Count(trade => !trade.IsClosed);
    public int Wins => Trades.Count(trade => trade.IsClosed && trade.ProfitLoss > 0m);
    public int Losses => Trades.Count(trade => trade.IsClosed && trade.ProfitLoss < 0m);
    public decimal WinRate => ClosedCount == 0 ? 0m : decimal.Round((decimal)Wins / ClosedCount * 100m, 2);

    private (decimal Amount, decimal Percent) CalculateMaxDrawdown()
    {
        var peak = InitialBudget;
        var maxDrawdown = 0m;

        foreach (var point in EquityCurve)
        {
            if (point.Equity > peak)
            {
                peak = point.Equity;
            }

            var drawdown = peak - point.Equity;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        var percent = peak <= 0m ? 0m : decimal.Round(maxDrawdown / peak * 100m, 2);
        return (decimal.Round(maxDrawdown, 8), percent);
    }

    public IReadOnlyList<PaperPortfolioEquityPoint> EquityCurve
    {
        get
        {
            var points = new List<PaperPortfolioEquityPoint>
            {
                new(FirstTradeAt ?? CreatedAt.UtcDateTime, InitialBudget, 0m, "Start")
            };

            foreach (var trade in Trades.OrderBy(trade => trade.EntryTime))
            {
                points.Add(new PaperPortfolioEquityPoint(
                    trade.ExitTime ?? trade.EntryTime,
                    trade.CashAfter + trade.CurrentValue,
                    trade.ProfitLoss,
                    trade.Symbol));
            }

            return points;
        }
    }
}

public sealed record PaperPortfolioEquityPoint(
    DateTime Time,
    decimal Equity,
    decimal ProfitLoss,
    string Label);

public sealed record PaperPortfolioTrade(
    long SignalId,
    string Symbol,
    string Timeframe,
    DateTime EntryTime,
    decimal EntryPrice,
    decimal Units,
    decimal Invested,
    decimal RemainingUnits,
    decimal CashBefore,
    decimal CashAfter,
    decimal EntryFee,
    decimal ExitFee,
    decimal SlippageCost,
    DateTime? ExitTime,
    decimal? ExitPrice,
    decimal CurrentPrice,
    decimal? BreakEvenStop,
    PaperTradeOutcome Outcome)
{
    public bool IsClosed => Outcome is PaperTradeOutcome.TakeProfit1 or PaperTradeOutcome.TakeProfit2 or PaperTradeOutcome.StopLoss or PaperTradeOutcome.Expired;
    public decimal CurrentValue => IsClosed ? 0m : RemainingUnits * CurrentPrice;
    public decimal TotalFees => EntryFee + ExitFee;
    public decimal ProfitLoss => IsClosed
        ? CashAfter - CashBefore
        : CashAfter + CurrentValue - CashBefore;
    public decimal ProfitLossPercent => Invested <= 0m ? 0m : decimal.Round(ProfitLoss / Invested * 100m, 2);
}
