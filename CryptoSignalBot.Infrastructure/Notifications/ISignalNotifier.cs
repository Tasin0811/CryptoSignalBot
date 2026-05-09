using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Infrastructure.Notifications;

public interface ISignalNotifier
{
    Task SendAsync(Signal signal, CancellationToken cancellationToken = default);
    Task SendReportAsync(WatchlistReport report, CancellationToken cancellationToken = default);
    Task SendPaperTradeReportAsync(PaperTradeReport report, CancellationToken cancellationToken = default);
}
