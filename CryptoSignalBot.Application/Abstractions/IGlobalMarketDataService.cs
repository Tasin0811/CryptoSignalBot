using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Abstractions;

public interface IGlobalMarketDataService
{
    Task<GlobalMarketData?> GetGlobalMarketDataAsync(CancellationToken cancellationToken = default);
}
