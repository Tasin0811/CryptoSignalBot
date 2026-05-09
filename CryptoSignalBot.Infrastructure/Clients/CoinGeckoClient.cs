using System.Text.Json;
using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.Market;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Infrastructure.Clients;

public sealed class CoinGeckoClient(HttpClient httpClient, IOptions<CoinGeckoSettings> settings) : IGlobalMarketDataService
{
    public async Task<GlobalMarketData?> GetGlobalMarketDataAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Value.Enabled)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildGlobalUri());
        if (!string.IsNullOrWhiteSpace(settings.Value.ApiKey))
        {
            request.Headers.Add("x-cg-demo-api-key", settings.Value.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var data = document.RootElement.GetProperty("data");
        return new GlobalMarketData(
            GetNestedDecimal(data, "total_market_cap", "usd"),
            GetNestedDecimal(data, "total_volume", "usd"),
            GetDecimal(data, "market_cap_change_percentage_24h_usd"),
            GetNestedDecimal(data, "market_cap_percentage", "btc"),
            GetNestedDecimal(data, "market_cap_percentage", "eth"),
            GetInt(data, "active_cryptocurrencies"),
            GetUpdatedAt(data));
    }

    private Uri BuildGlobalUri()
    {
        var baseUri = new Uri(settings.Value.BaseUrl.TrimEnd('/') + "/");
        return new Uri(baseUri, "global");
    }

    private static decimal? GetNestedDecimal(JsonElement data, string propertyName, string childPropertyName)
    {
        if (!data.TryGetProperty(propertyName, out var parent) ||
            !parent.TryGetProperty(childPropertyName, out var child))
        {
            return null;
        }

        return child.ValueKind == JsonValueKind.Number ? child.GetDecimal() : null;
    }

    private static decimal? GetDecimal(JsonElement data, string propertyName)
    {
        return data.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;
    }

    private static int? GetInt(JsonElement data, string propertyName)
    {
        return data.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static DateTimeOffset? GetUpdatedAt(JsonElement data)
    {
        return data.TryGetProperty("updated_at", out var value) && value.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(value.GetInt64())
            : null;
    }
}
