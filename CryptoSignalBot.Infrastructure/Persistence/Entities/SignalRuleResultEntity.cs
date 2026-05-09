namespace CryptoSignalBot.Infrastructure.Persistence.Entities;

public sealed class SignalRuleResultEntity
{
    public long Id { get; set; }

    public long SignalId { get; set; }

    public SignalEntity Signal { get; set; } = null!;

    public string RuleName { get; set; } = string.Empty;

    public decimal ScoreImpact { get; set; }

    public string Result { get; set; } = string.Empty;

    public string? Details { get; set; }
}
