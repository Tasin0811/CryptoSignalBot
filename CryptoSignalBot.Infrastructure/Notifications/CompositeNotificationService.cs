using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Reports;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Infrastructure.Notifications;

public sealed class CompositeNotificationService(IEnumerable<ISignalNotifier> notifiers) : INotificationService
{
    public async Task SendSignalAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        foreach (var notifier in notifiers)
        {
            await notifier.SendAsync(signal, cancellationToken);
        }
    }

    public async Task SendReportAsync(WatchlistReport report, CancellationToken cancellationToken = default)
    {
        foreach (var notifier in notifiers)
        {
            await notifier.SendReportAsync(report, cancellationToken);
        }
    }

    public async Task SendPaperTradeReportAsync(PaperTradeReport report, CancellationToken cancellationToken = default)
    {
        foreach (var notifier in notifiers)
        {
            await notifier.SendPaperTradeReportAsync(report, cancellationToken);
        }
    }
}
