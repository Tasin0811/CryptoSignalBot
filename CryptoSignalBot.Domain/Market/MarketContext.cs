namespace CryptoSignalBot.Domain.Market;

public sealed record MarketContext(
    bool IsBtcRiskOff,
    bool IsAltcoinContextPositive,
    decimal ScoreImpact,
    string Summary);
