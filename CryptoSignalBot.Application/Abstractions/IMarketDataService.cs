using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Abstractions;

public interface IMarketDataService
{
    Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string symbol,
        string timeframe,
        int limit,
        CancellationToken cancellationToken = default);
}
