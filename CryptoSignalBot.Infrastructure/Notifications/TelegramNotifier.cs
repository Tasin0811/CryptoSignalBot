using System.Net.Http.Json;
using System.Globalization;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.Signals;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Infrastructure.Notifications;

public sealed class TelegramNotifier(HttpClient httpClient, IOptions<TelegramSettings> settings) : ISignalNotifier
{
    public async Task SendAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.BotToken) ||
            string.IsNullOrWhiteSpace(settings.Value.ChatId) ||
            settings.Value.BotToken.StartsWith("ENV:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var payload = new
        {
            chat_id = settings.Value.ChatId,
            text = FormatSignal(signal),
            parse_mode = "Markdown"
        };

        using var response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{settings.Value.BotToken}/sendMessage",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task SendReportAsync(WatchlistReport report, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.BotToken) ||
            string.IsNullOrWhiteSpace(settings.Value.ChatId) ||
            settings.Value.BotToken.StartsWith("ENV:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var payload = new
        {
            chat_id = settings.Value.ChatId,
            text = FormatReport(report),
            parse_mode = "Markdown"
        };

        using var response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{settings.Value.BotToken}/sendMessage",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public Task SendPaperTradeReportAsync(PaperTradeReport report, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static string FormatSignal(Signal signal)
    {
        return SignalMessageFormatter.FormatBeginnerTelegram(signal);
    }

    private static string FormatReport(WatchlistReport report)
    {
        var topSignals = report.NotifiableSignals.Take(report.MaxSetups).ToArray();
        if (topSignals.Length == 0)
        {
            return $"*CryptoSignalBot Report*\n{report.CreatedAt:yyyy-MM-dd HH:mm} UTC\nNo signals above threshold {report.MinScoreToNotify}/10.";
        }

        return $"*CryptoSignalBot Report*\n{report.CreatedAt:yyyy-MM-dd HH:mm} UTC\n" +
               $"Top setups: {topSignals.Length}\nSuppressed duplicates: {report.SuppressedDuplicates}\n\n" +
               string.Join("\n", topSignals.Select(signal =>
                   $"{signal.Symbol} {signal.Timeframe} - {SignalMessageFormatter.FormatSignalType(signal.SignalType)} {signal.Score.ToString("0.##", CultureInfo.InvariantCulture)}/10\n{SignalMessageFormatter.FormatReportActionLine(signal)}"));
    }
}
