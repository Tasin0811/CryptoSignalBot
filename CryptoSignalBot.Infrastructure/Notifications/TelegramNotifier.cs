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

    public async Task SendPaperTradeReportAsync(PaperTradeReport report, CancellationToken cancellationToken = default)
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
            text = FormatPaperTradeReport(report),
            parse_mode = "Markdown"
        };

        using var response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{settings.Value.BotToken}/sendMessage",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
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

    private static string FormatPaperTradeReport(PaperTradeReport report)
    {
        var lines = new List<string>
        {
            "*CryptoSignalBot Paper Trading*",
            $"{report.CreatedAt:yyyy-MM-dd HH:mm} UTC",
            $"Signals: {report.Results.Count} | Closed: {report.ClosedCount} | Open: {report.OpenCount}",
            $"Wins: {report.Wins} | Losses: {report.Losses} | Win rate: {report.WinRate.ToString("0.##", CultureInfo.InvariantCulture)}%",
            ""
        };

        var recentClosedTrades = report.RecentClosedTrades(5);
        if (recentClosedTrades.Count == 0)
        {
            lines.Add("No closed paper trades yet.");
            return string.Join("\n", lines);
        }

        lines.Add("Recent closed:");
        foreach (var trade in recentClosedTrades)
        {
            lines.Add(
                $"{trade.Symbol} {trade.Timeframe} {FormatOutcome(trade.Outcome)} " +
                $"{FormatReturn(trade.ReturnPercent)} score {trade.Score.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        return string.Join("\n", lines);
    }

    private static string FormatOutcome(PaperTradeOutcome outcome)
    {
        return outcome switch
        {
            PaperTradeOutcome.TakeProfit1 => "WIN",
            PaperTradeOutcome.StopLoss => "LOSS",
            _ => outcome.ToString()
        };
    }

    private static string FormatReturn(decimal? value)
    {
        return value.HasValue
            ? $"{value.Value.ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture)}%"
            : "n/a";
    }
}
