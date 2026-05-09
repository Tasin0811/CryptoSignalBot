using CryptoSignalBot.Domain.Indicators;
using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Abstractions;

public interface IIndicatorEngine
{
    IndicatorSnapshot Calculate(IReadOnlyList<Candle> candles);
}
