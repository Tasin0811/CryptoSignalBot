namespace CryptoSignalBot.Infrastructure.Persistence.Entities;

public sealed class PaperTradeEntity
{
    public long Id { get; set; }

    public long SignalId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Timeframe { get; set; } = string.Empty;

    public DateTime EntryTime { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal Units { get; set; }

    public decimal Invested { get; set; }

    public decimal RemainingUnits { get; set; }

    public decimal CashBefore { get; set; }

    public decimal CashAfter { get; set; }

    public decimal EntryFee { get; set; }

    public decimal ExitFee { get; set; }

    public decimal SlippageCost { get; set; }

    public DateTime? ExitTime { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal? BreakEvenStop { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
