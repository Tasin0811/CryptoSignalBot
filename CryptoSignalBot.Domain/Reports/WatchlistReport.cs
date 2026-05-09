using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Domain.Reports;

public sealed record WatchlistReport(
    DateTimeOffset CreatedAt,
    decimal MinScoreToNotify,
    IReadOnlyList<Signal> Signals,
    int AnalyzedCount,
    int MaxSetups = 8,
    int SuppressedDuplicates = 0)
{
    public IReadOnlyList<Signal> NotifiableSignals =>
        Signals.Where(signal => signal.Score >= MinScoreToNotify)
            .OrderByDescending(signal => signal.Score)
            .ThenBy(signal => signal.Symbol)
            .ThenBy(signal => signal.Timeframe)
            .ToArray();
}
