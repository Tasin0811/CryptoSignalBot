using CryptoSignalBot.Domain.Backtesting;

namespace CryptoSignalBot.Application.Abstractions;

public interface IBacktestService
{
    Task<BacktestReport> RunAsync(BacktestOptions options, CancellationToken cancellationToken = default);
}
