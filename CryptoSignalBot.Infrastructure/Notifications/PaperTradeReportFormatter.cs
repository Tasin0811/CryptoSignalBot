using CryptoSignalBot.Domain.PaperTrading;

namespace CryptoSignalBot.Infrastructure.Notifications;

public static class PaperTradeReportFormatter
{
    public static string FormatSubject(PaperTradeReport report)
    {
        return $"CryptoSignalBot Paper Trading - {report.Wins}W/{report.Losses}L - {report.CreatedAt:yyyy-MM-dd HH:mm}";
    }

    public static string FormatText(PaperTradeReport report, int recentClosedCount = 10)
    {
        var lines = new List<string>
        {
            "CryptoSignalBot Paper Trading Report",
            $"{report.CreatedAt:yyyy-MM-dd HH:mm} UTC",
            "",
            "SUMMARY",
            $"Evaluated signals: {report.Results.Count}",
            $"Closed: {report.ClosedCount}",
            $"Wins: {report.Wins}",
            $"Losses: {report.Losses}",
            $"Open: {report.OpenCount}",
            $"Expired: {report.ExpiredCount}",
            $"Win rate: {report.WinRate:0.##}%",
            ""
        };

        if (report.InvalidCount > 0)
        {
            lines.Add($"Invalid: {report.InvalidCount}");
            lines.Add("");
        }

        lines.Add("RECENT CLOSED TRADES");
        var recentClosedTrades = report.RecentClosedTrades(recentClosedCount);
        if (recentClosedTrades.Count == 0)
        {
            lines.Add("No closed paper trades yet.");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var trade in recentClosedTrades)
        {
            lines.Add(
                $"{trade.ExitTime:yyyy-MM-dd HH:mm} {trade.Symbol} {trade.Timeframe} {trade.SignalType} " +
                $"score {trade.Score:0.##}: {FormatOutcome(trade.Outcome)} " +
                $"entry {trade.EntryPrice:0.########} exit {FormatNullable(trade.ExitPrice)} return {FormatReturn(trade.ReturnPercent)}");
        }

        return string.Join(Environment.NewLine, lines);
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

    private static string FormatNullable(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.########") : "n/a";
    }

    private static string FormatReturn(decimal? value)
    {
        return value.HasValue ? $"{value.Value:+0.####;-0.####;0}%" : "n/a";
    }
}
