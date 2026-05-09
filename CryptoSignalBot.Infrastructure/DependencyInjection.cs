using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Infrastructure.Clients;
using CryptoSignalBot.Infrastructure.Notifications;
using CryptoSignalBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoSignalBot.Infrastructure;

public static class DependencyInjection
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=CryptoSignalBot;Trusted_Connection=True;TrustServerCertificate=True;";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var binanceSettings = configuration.GetSection("Binance").Get<BinanceSettings>() ?? new BinanceSettings();
        var coinGeckoSettings = configuration.GetSection("CoinGecko").Get<CoinGeckoSettings>() ?? new CoinGeckoSettings();

        services.AddSingleton(binanceSettings);
        services.AddSingleton(coinGeckoSettings);
        services.Configure<BinanceSettings>(configuration.GetSection("Binance"));
        services.Configure<CoinGeckoSettings>(configuration.GetSection("CoinGecko"));
        services.Configure<TelegramSettings>(configuration.GetSection("Telegram"));
        services.Configure<EmailSettings>(configuration.GetSection("Email"));

        var connectionString =
            configuration.GetConnectionString("CryptoSignalBot") ??
            configuration.GetConnectionString("DefaultConnection") ??
            DefaultConnectionString;
        var databaseProvider = configuration["Database:Provider"];

        services.AddDbContext<CryptoSignalBotDbContext>(options =>
        {
            if (IsSqlite(databaseProvider, connectionString))
            {
                options.UseSqlite(connectionString);
                return;
            }

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IPersistenceService, EfPersistenceService>();
        services.AddHttpClient<IMarketDataService, BinanceRestClient>();
        if (coinGeckoSettings.Enabled)
        {
            services.AddHttpClient<IGlobalMarketDataService, CoinGeckoClient>();
        }

        services.AddHttpClient<TelegramNotifier>();
        services.AddScoped<ISignalNotifier>(provider => provider.GetRequiredService<TelegramNotifier>());
        services.AddScoped<ISignalNotifier, EmailNotifier>();
        services.AddScoped<INotificationService, CompositeNotificationService>();

        return services;
    }

    private static bool IsSqlite(string? provider, string connectionString)
    {
        return string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
               connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
    }
}
