using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Abstractions;

public interface IMarketContextEngine
{
    MarketContext Evaluate(
        IReadOnlyList<Candle> btcCandles,
        IReadOnlyList<Candle> benchmarkCandles,
        GlobalMarketData? globalMarketData = null);
}
