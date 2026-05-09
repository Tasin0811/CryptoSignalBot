using System.Net;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Infrastructure.Clients;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Application.Tests;

public sealed class CoinGeckoClientTests
{
    [Fact]
    public async Task GetGlobalMarketDataAsync_ParsesCoinGeckoGlobalPayload()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "data": {
                    "active_cryptocurrencies": 12345,
                    "total_market_cap": { "usd": 2500000000000.5 },
                    "total_volume": { "usd": 90000000000.25 },
                    "market_cap_percentage": { "btc": 53.2, "eth": 17.4 },
                    "market_cap_change_percentage_24h_usd": -1.75,
                    "updated_at": 1710000000
                  }
                }
                """)
        }));
        var client = new CoinGeckoClient(httpClient, Options.Create(new CoinGeckoSettings
        {
            BaseUrl = "https://example.test/api/v3",
            ApiKey = "demo-key"
        }));

        var data = await client.GetGlobalMarketDataAsync();

        Assert.NotNull(data);
        Assert.Equal(2_500_000_000_000.5m, data.TotalMarketCapUsd);
        Assert.Equal(90_000_000_000.25m, data.TotalVolumeUsd);
        Assert.Equal(-1.75m, data.MarketCapChangePercentage24hUsd);
        Assert.Equal(53.2m, data.BitcoinDominancePercentage);
        Assert.Equal(17.4m, data.EthereumDominancePercentage);
        Assert.Equal(12_345, data.ActiveCryptocurrencies);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_710_000_000), data.UpdatedAt);
    }

    [Fact]
    public async Task GetGlobalMarketDataAsync_WhenDisabled_SkipsHttpCall()
    {
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var client = new CoinGeckoClient(httpClient, Options.Create(new CoinGeckoSettings
        {
            Enabled = false
        }));

        var data = await client.GetGlobalMarketDataAsync();

        Assert.Null(data);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://example.test/api/v3/global", request.RequestUri?.ToString());
            Assert.True(request.Headers.Contains("x-cg-demo-api-key"));

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("HTTP should not be called when CoinGecko is disabled.");
        }
    }
}
