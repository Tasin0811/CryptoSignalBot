using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.Signals;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CryptoSignalBot.Infrastructure.Notifications;

public sealed class EmailNotifier(IOptions<EmailSettings> settings) : ISignalNotifier
{
    public async Task SendAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        var email = settings.Value;
        if (string.IsNullOrWhiteSpace(email.SmtpHost) ||
            string.IsNullOrWhiteSpace(email.From) ||
            string.IsNullOrWhiteSpace(email.To) ||
            email.Username.StartsWith("ENV:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(email.From));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = $"{signal.Symbol} - {SignalMessageFormatter.FormatSignalType(signal.SignalType)} ({signal.Score:0.##}/10)";
        message.Body = new TextPart("plain")
        {
            Text = SignalMessageFormatter.FormatBeginnerEmail(signal)
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(email.SmtpHost, email.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        await client.AuthenticateAsync(email.Username, email.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendReportAsync(WatchlistReport report, CancellationToken cancellationToken = default)
    {
        var email = settings.Value;
        if (string.IsNullOrWhiteSpace(email.SmtpHost) ||
            string.IsNullOrWhiteSpace(email.From) ||
            string.IsNullOrWhiteSpace(email.To) ||
            email.Username.StartsWith("ENV:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(email.From));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = $"CryptoSignalBot Report - {report.NotifiableSignals.Count} top setup - {report.CreatedAt:yyyy-MM-dd HH:mm}";
        message.Body = new TextPart("plain")
        {
            Text = FormatReport(report)
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(email.SmtpHost, email.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        await client.AuthenticateAsync(email.Username, email.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPaperTradeReportAsync(PaperTradeReport report, CancellationToken cancellationToken = default)
    {
        var email = settings.Value;
        if (string.IsNullOrWhiteSpace(email.SmtpHost) ||
            string.IsNullOrWhiteSpace(email.From) ||
            string.IsNullOrWhiteSpace(email.To) ||
            email.Username.StartsWith("ENV:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(email.From));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = PaperTradeReportFormatter.FormatSubject(report);
        message.Body = new TextPart("plain")
        {
            Text = PaperTradeReportFormatter.FormatText(report)
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(email.SmtpHost, email.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        await client.AuthenticateAsync(email.Username, email.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static string FormatReport(WatchlistReport report)
    {
        var topByAsset = report.NotifiableSignals
            .GroupBy(signal => signal.Symbol)
            .Select(group => new
            {
                Symbol = group.Key,
                Best = group.OrderByDescending(signal => signal.Score).ThenBy(signal => signal.Timeframe).First(),
                Confirmations = group.OrderBy(signal => signal.Timeframe).ToArray()
            })
            .OrderByDescending(item => item.Best.Score)
            .ThenBy(item => item.Symbol)
            .Take(report.MaxSetups)
            .ToArray();

        var belowThreshold = report.Signals
            .Where(signal => signal.Score < report.MinScoreToNotify)
            .OrderByDescending(signal => signal.Score)
            .ThenBy(signal => signal.Symbol)
            .ThenBy(signal => signal.Timeframe)
            .Take(8)
            .ToArray();

        var lines = new List<string>
        {
            "CryptoSignalBot Report",
            $"{report.CreatedAt:yyyy-MM-dd HH:mm} UTC",
            $"Threshold: {report.MinScoreToNotify}/10",
            $"Analyzed: {report.AnalyzedCount}",
            $"Top setups: {topByAsset.Length}",
            $"Suppressed duplicates: {report.SuppressedDuplicates}",
            "",
            "This is an analytical alert, not financial advice. No automatic trade was executed.",
            ""
        };

        lines.Add("TOP SETUPS");
        if (topByAsset.Length == 0)
        {
            lines.Add("No fresh setup above threshold.");
        }

        foreach (var item in topByAsset)
        {
            var best = item.Best;
            var confirmations = string.Join(", ", item.Confirmations.Select(signal => $"{signal.Timeframe} {signal.Score:0.##}"));
            lines.Add($"{item.Symbol}");
            lines.Add($"Best: {best.Timeframe} {SignalMessageFormatter.FormatSignalType(best.SignalType)} {best.Score:0.##}/10");
            lines.Add($"Meaning: {SignalMessageFormatter.FormatReportActionLine(best)}");
            lines.Add($"Confirmations: {confirmations}");
            lines.Add($"Plan: price {best.Price:0.########} | stop {SignalMessageFormatter.FormatNullable(best.StopLoss)} | first target {SignalMessageFormatter.FormatNullable(best.TakeProfit1)} | R/R {SignalMessageFormatter.FormatNullable(best.RiskReward)}");
            lines.Add("");
        }

        lines.Add("BELOW THRESHOLD SNAPSHOT");
        foreach (var signal in belowThreshold)
        {
            lines.Add($"{signal.Symbol,-8} {signal.Timeframe,-3} {SignalMessageFormatter.FormatSignalType(signal.SignalType),-34} {signal.Score:0.##}/10");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
