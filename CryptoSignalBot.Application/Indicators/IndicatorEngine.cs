using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Indicators;
using CryptoSignalBot.Domain.Market;
using Skender.Stock.Indicators;

namespace CryptoSignalBot.Application.Indicators;

public sealed class IndicatorEngine : IIndicatorEngine
{
    public IndicatorSnapshot Calculate(IReadOnlyList<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);

        if (candles.Count == 0)
        {
            return new IndicatorSnapshot(null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        List<Candle> ordered = candles.OrderBy(c => c.OpenTime).ToList();
        List<Quote> quotes = ordered
            .Select(c => new Quote
            {
                Date = c.OpenTime,
                Open = c.OpenPrice,
                High = c.HighPrice,
                Low = c.LowPrice,
                Close = c.ClosePrice,
                Volume = c.Volume
            })
            .ToList();

        EmaResult? ema50 = quotes.GetEma(50).LastOrDefault();
        EmaResult? ema200 = quotes.GetEma(200).LastOrDefault();
        RsiResult? rsi = quotes.GetRsi(14).LastOrDefault();
        MacdResult? macd = quotes.GetMacd(12, 26, 9).LastOrDefault();
        AtrResult? atr = quotes.GetAtr(14).LastOrDefault();
        BollingerBandsResult? bollinger = quotes.GetBollingerBands(20, 2).LastOrDefault();
        AdxResult? adx = quotes.GetAdx(14).LastOrDefault();

        decimal? volumeSma20 = ordered.Count >= 20
            ? ordered.TakeLast(20).Average(c => c.Volume)
            : null;
        decimal latestVolume = ordered[^1].Volume;
        decimal? volumeRatio = volumeSma20 is > 0m ? latestVolume / volumeSma20.Value : null;
        var supportResistance = CalculateSupportResistance(ordered);

        return new IndicatorSnapshot(
            ToDecimal(ema50?.Ema),
            ToDecimal(ema200?.Ema),
            ToDecimal(rsi?.Rsi),
            ToDecimal(macd?.Macd),
            ToDecimal(macd?.Signal),
            ToDecimal(macd?.Histogram),
            ToDecimal(atr?.Atr),
            ToDecimal(bollinger?.UpperBand),
            ToDecimal(bollinger?.Sma),
            ToDecimal(bollinger?.LowerBand),
            ToDecimal(adx?.Adx),
            volumeSma20,
            volumeRatio,
            supportResistance.Support,
            supportResistance.Resistance,
            supportResistance.SupportDistancePercent,
            supportResistance.ResistanceDistancePercent);
    }

    private static decimal? ToDecimal(double? value)
    {
        return value.HasValue ? Convert.ToDecimal(value.Value) : null;
    }

    private static SupportResistanceSnapshot CalculateSupportResistance(IReadOnlyList<Candle> orderedCandles)
    {
        if (orderedCandles.Count < 2)
        {
            return new SupportResistanceSnapshot(null, null, null, null);
        }

        var price = orderedCandles[^1].ClosePrice;
        if (price <= 0m)
        {
            return new SupportResistanceSnapshot(null, null, null, null);
        }

        var recentCompletedCandles = orderedCandles
            .Take(orderedCandles.Count - 1)
            .TakeLast(50)
            .ToArray();

        var support = recentCompletedCandles
            .Where(candle => candle.LowPrice <= price)
            .Select(candle => (decimal?)candle.LowPrice)
            .Max();

        var resistance = recentCompletedCandles
            .Where(candle => candle.HighPrice >= price)
            .Select(candle => (decimal?)candle.HighPrice)
            .Min();

        decimal? supportDistance = support.HasValue
            ? (price - support.Value) / price * 100m
            : null;
        decimal? resistanceDistance = resistance.HasValue
            ? (resistance.Value - price) / price * 100m
            : null;

        return new SupportResistanceSnapshot(support, resistance, supportDistance, resistanceDistance);
    }

    private sealed record SupportResistanceSnapshot(
        decimal? Support,
        decimal? Resistance,
        decimal? SupportDistancePercent,
        decimal? ResistanceDistancePercent);
}
