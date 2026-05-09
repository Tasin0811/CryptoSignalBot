using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Application.Backtesting;
using CryptoSignalBot.Application.Indicators;
using CryptoSignalBot.Application.Market;
using CryptoSignalBot.Application.PaperTrading;
using CryptoSignalBot.Application.Risk;
using CryptoSignalBot.Application.Signals;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoSignalBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIndicatorEngine, IndicatorEngine>();
        services.AddScoped<IMarketContextEngine, MarketContextEngine>();
        services.AddScoped<IRiskEngine, RiskEngine>();
        services.AddScoped<ISignalEngine, SignalEngine>();
        services.AddScoped<IPaperTradingService, PaperTradingService>();
        services.AddScoped<IBacktestService, BacktestService>();

        return services;
    }
}
