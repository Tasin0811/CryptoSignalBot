namespace CryptoSignalBot.Domain.Market;

public sealed record GlobalMarketData(
    decimal? TotalMarketCapUsd,
    decimal? TotalVolumeUsd,
    decimal? MarketCapChangePercentage24hUsd,
    decimal? BitcoinDominancePercentage,
    decimal? EthereumDominancePercentage,
    int? ActiveCryptocurrencies,
    DateTimeOffset? UpdatedAt);
