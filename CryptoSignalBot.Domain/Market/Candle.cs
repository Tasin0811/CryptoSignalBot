namespace CryptoSignalBot.Domain.Market;

public sealed record Candle(
    string Symbol,
    string Timeframe,
    DateTime OpenTime,
    DateTime CloseTime,
    decimal OpenPrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal ClosePrice,
    decimal Volume);
