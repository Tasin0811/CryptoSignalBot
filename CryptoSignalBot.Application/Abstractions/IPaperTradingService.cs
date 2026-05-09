using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Abstractions;

public interface IPaperTradingService
{
    PaperTradeResult Evaluate(Signal signal, IReadOnlyList<Candle> futureCandles, int maxCandles);
}
