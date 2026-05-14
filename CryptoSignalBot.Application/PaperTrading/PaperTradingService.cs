using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.PaperTrading;

public sealed class PaperTradingService : IPaperTradingService
{
    public PaperTradeResult Evaluate(Signal signal, IReadOnlyList<Candle> futureCandles, int maxCandles)
    {
        if (!signal.StopLoss.HasValue || !signal.TakeProfit1.HasValue || signal.Price <= 0)
        {
            return Create(signal, PaperTradeOutcome.Invalid, null, null);
        }

        var ordered = futureCandles
            .Where(candle => candle.OpenTime > signal.CreatedAt.UtcDateTime)
            .OrderBy(candle => candle.OpenTime)
            .Take(maxCandles)
            .ToArray();

        var tp1Hit = false;
        foreach (var candle in ordered)
        {
            if (!tp1Hit && candle.LowPrice <= signal.StopLoss.Value)
            {
                return Create(signal, PaperTradeOutcome.StopLoss, candle.OpenTime, signal.StopLoss.Value);
            }

            if (!tp1Hit && candle.HighPrice >= signal.TakeProfit1.Value)
            {
                tp1Hit = true;
            }

            if (tp1Hit && candle.LowPrice <= signal.Price)
            {
                return Create(signal, PaperTradeOutcome.TakeProfit1, candle.OpenTime, signal.Price);
            }

            if (tp1Hit && signal.TakeProfit2.HasValue && candle.HighPrice >= signal.TakeProfit2.Value)
            {
                return Create(signal, PaperTradeOutcome.TakeProfit2, candle.OpenTime, signal.TakeProfit2.Value);
            }
        }

        return ordered.Length < maxCandles
            ? Create(signal, PaperTradeOutcome.Open, null, null)
            : Create(signal, PaperTradeOutcome.Expired, ordered[^1].OpenTime, ordered[^1].ClosePrice);
    }

    private static PaperTradeResult Create(Signal signal, PaperTradeOutcome outcome, DateTime? exitTime, decimal? exitPrice)
    {
        decimal? returnPercent = exitPrice.HasValue
            ? decimal.Round((exitPrice.Value - signal.Price) / signal.Price * 100m, 4)
            : null;

        return new PaperTradeResult(
            0,
            signal.Symbol,
            signal.Timeframe,
            signal.CreatedAt.UtcDateTime,
            signal.Price,
            signal.StopLoss,
            signal.TakeProfit1,
            signal.Score,
            signal.SignalType.ToString(),
            outcome,
            exitTime,
            exitPrice,
            returnPercent);
    }
}
