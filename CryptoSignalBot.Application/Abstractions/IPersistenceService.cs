using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Abstractions;

public interface IPersistenceService
{
    Task SaveCandlesAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken = default);
    Task SaveSignalAsync(Signal signal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Candle>> GetCandlesAsync(string symbol, string timeframe, int maxCandles, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Signal>> GetSignalsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
    Task<PaperTradeReport> BuildPaperTradeReportAsync(int maxSignals, int maxFutureCandles, CancellationToken cancellationToken = default);
    Task<PaperPortfolioReport> BuildPaperPortfolioReportAsync(decimal initialBudget, int maxSignals, int maxFutureCandles, CancellationToken cancellationToken = default);
    Task<int> CleanupDatabaseAsync(DateTimeOffset retainCandlesSince, DateTimeOffset retainSignalsSince, CancellationToken cancellationToken = default);
}
