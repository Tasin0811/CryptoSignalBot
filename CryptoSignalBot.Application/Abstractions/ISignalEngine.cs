using CryptoSignalBot.Domain.Indicators;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Abstractions;

public interface ISignalEngine
{
    Signal Analyze(
        string symbol,
        string timeframe,
        decimal price,
        IndicatorSnapshot indicators,
        MarketContext marketContext,
        RiskPlan riskPlan);
}
