using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Abstractions;

public interface INotificationService
{
    Task SendSignalAsync(Signal signal, CancellationToken cancellationToken = default);
    Task SendReportAsync(WatchlistReport report, CancellationToken cancellationToken = default);
    Task SendPaperTradeReportAsync(PaperTradeReport report, CancellationToken cancellationToken = default);
}
