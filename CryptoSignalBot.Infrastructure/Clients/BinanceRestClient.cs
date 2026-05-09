using System.Globalization;
using System.Text.Json;
using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.Market;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Infrastructure.Clients;

public sealed class BinanceRestClient(HttpClient httpClient, IOptions<BinanceSettings> settings) : IMarketDataService
{
    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string symbol,
        string timeframe,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeframe);

        var requestUri = BuildKlinesUri(symbol, timeframe, limit);
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var candles = new List<Candle>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            candles.Add(MapKline(symbol, timeframe, item));
        }

        return candles;
    }

    private Uri BuildKlinesUri(string symbol, string interval, int limit)
    {
        var baseUri = new Uri(settings.Value.BaseUrl.TrimEnd('/') + "/");
        var relative = $"api/v3/klines?symbol={Uri.EscapeDataString(symbol.ToUpperInvariant())}&interval={Uri.EscapeDataString(interval)}&limit={limit}";
        return new Uri(baseUri, relative);
    }

    private static Candle MapKline(string symbol, string timeframe, JsonElement item)
    {
        return new Candle(
            symbol.ToUpperInvariant(),
            timeframe,
            FromUnixMilliseconds(item[0].GetInt64()),
            FromUnixMilliseconds(item[6].GetInt64()),
            GetDecimal(item[1]),
            GetDecimal(item[2]),
            GetDecimal(item[3]),
            GetDecimal(item[4]),
            GetDecimal(item[5]));
    }

    private static DateTime FromUnixMilliseconds(long value)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;
    }

    private static decimal GetDecimal(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String => decimal.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => throw new JsonException($"Unexpected Binance kline numeric value kind '{element.ValueKind}'.")
        };
    }
}
