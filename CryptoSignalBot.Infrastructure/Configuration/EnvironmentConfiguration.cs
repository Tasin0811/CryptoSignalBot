using Microsoft.Extensions.Configuration;

namespace CryptoSignalBot.Infrastructure.Configuration;

public static class EnvironmentConfiguration
{
    public static IConfigurationBuilder AddCryptoSignalBotEnvironmentVariables(this IConfigurationBuilder builder)
    {
        var values = new Dictionary<string, string?>();
        AddIfSet(values, "TELEGRAM_BOT_TOKEN", "Telegram:BotToken");
        AddIfSet(values, "TELEGRAM_CHAT_ID", "Telegram:ChatId");
        AddIfSet(values, "SMTP_USER", "Email:Username");
        AddIfSet(values, "SMTP_PASSWORD", "Email:Password");
        AddIfSet(values, "SMTP_FROM", "Email:From");
        AddIfSet(values, "SMTP_TO", "Email:To");
        AddIfSet(values, "DATABASE_CONNECTION_STRING", "ConnectionStrings:CryptoSignalBot");
        AddIfSet(values, "COINGECKO_API_KEY", "CoinGecko:ApiKey");

        if (values.Count > 0)
        {
            builder.AddInMemoryCollection(values);
        }

        return builder;
    }

    private static void AddIfSet(IDictionary<string, string?> values, string environmentName, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[configurationKey] = value;
        }
    }
}
