namespace CryptoSignalBot.Infrastructure.Persistence.Entities;

public sealed class SignalEntity
{
    public long Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Timeframe { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public decimal Price { get; set; }

    public decimal Score { get; set; }

    public string SignalType { get; set; } = string.Empty;

    public decimal? StopLoss { get; set; }

    public decimal? TakeProfit1 { get; set; }

    public decimal? TakeProfit2 { get; set; }

    public decimal? RiskReward { get; set; }

    public string Summary { get; set; } = string.Empty;

    public ICollection<SignalRuleResultEntity> RuleResults { get; } = [];
}
