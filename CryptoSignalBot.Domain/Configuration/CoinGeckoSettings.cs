namespace CryptoSignalBot.Domain.Configuration;

public sealed class CoinGeckoSettings
{
    public bool Enabled { get; init; } = true;
    public string BaseUrl { get; init; } = "https://api.coingecko.com/api/v3";
    public string? ApiKey { get; init; }
}
