using System.Globalization;
using System.Text;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Dashboard;

internal static class CsvExporter
{
    public static string FormatSignals(IEnumerable<Signal> signals)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CreatedAt,Symbol,Timeframe,SignalType,Score,Price,RiskLevel,StopLoss,TakeProfit1,TakeProfit2,RiskReward,Summary");

        foreach (var signal in signals)
        {
            builder.AppendLine(string.Join(
                ",",
                Escape(signal.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
                Escape(signal.Symbol),
                Escape(signal.Timeframe),
                Escape(signal.SignalType.ToString()),
                Escape(FormatDecimal(signal.Score)),
                Escape(FormatDecimal(signal.Price)),
                Escape(signal.RiskLevel.ToString()),
                Escape(FormatNullable(signal.StopLoss)),
                Escape(FormatNullable(signal.TakeProfit1)),
                Escape(FormatNullable(signal.TakeProfit2)),
                Escape(FormatNullable(signal.RiskReward)),
                Escape(signal.Summary)));
        }

        return builder.ToString();
    }

    private static string FormatNullable(decimal? value)
    {
        return value.HasValue ? FormatDecimal(value.Value) : "";
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
