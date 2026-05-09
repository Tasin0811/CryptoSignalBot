namespace CryptoSignalBot.Domain.Indicators;

public sealed record IndicatorSnapshot(
    decimal? Ema50,
    decimal? Ema200,
    decimal? Rsi14,
    decimal? Macd,
    decimal? MacdSignal,
    decimal? MacdHistogram,
    decimal? Atr14,
    decimal? BollingerUpper,
    decimal? BollingerMiddle,
    decimal? BollingerLower,
    decimal? Adx14,
    decimal? VolumeSma20,
    decimal? VolumeRatio,
    decimal? SupportLevel = null,
    decimal? ResistanceLevel = null,
    decimal? SupportDistancePercent = null,
    decimal? ResistanceDistancePercent = null);
