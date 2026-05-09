using CryptoSignalBot.Application.Indicators;
using CryptoSignalBot.Domain.Market;

namespace CryptoSignalBot.Application.Tests;

public sealed class IndicatorEngineSupportResistanceTests
{
    [Fact]
    public void Calculate_DerivesNearestSupportAndResistanceFromRecentCompletedCandles()
    {
        var engine = new IndicatorEngine();
        var candles = new[]
        {
            CreateCandle(0, 100m, 106m, 98m, 102m),
            CreateCandle(1, 102m, 105m, 101m, 103m),
            CreateCandle(2, 103m, 108m, 103m, 104m),
            CreateCandle(3, 104m, 110m, 102m, 104m)
        };

        var snapshot = engine.Calculate(candles);

        Assert.Equal(103m, snapshot.SupportLevel);
        Assert.Equal(105m, snapshot.ResistanceLevel);
        Assert.Equal(0.9615384615384615384615384600m, snapshot.SupportDistancePercent);
        Assert.Equal(0.9615384615384615384615384600m, snapshot.ResistanceDistancePercent);
    }

    private static Candle CreateCandle(int hour, decimal open, decimal high, decimal low, decimal close)
    {
        var openTime = new DateTime(2026, 5, 9, hour, 0, 0, DateTimeKind.Utc);
        return new Candle("BTCUSDT", "1h", openTime, openTime.AddHours(1), open, high, low, close, 1000m);
    }
}
