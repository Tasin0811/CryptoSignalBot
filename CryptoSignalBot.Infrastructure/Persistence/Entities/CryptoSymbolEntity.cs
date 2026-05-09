namespace CryptoSignalBot.Infrastructure.Persistence.Entities;

public sealed class CryptoSymbolEntity
{
    public int Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public decimal MinScoreToNotify { get; set; } = 7.50m;
}
