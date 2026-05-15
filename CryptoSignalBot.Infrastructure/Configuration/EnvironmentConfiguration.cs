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
        AddArrayIfSet(values, "BOT_TIMEFRAMES", "Bot:Timeframes");
        AddArrayIfSet(values, "BOT_REPORT_TIMEFRAMES", "Bot:ReportTimeframes");
        AddIfSet(values, "BOT_MIN_SCORE_TO_NOTIFY", "Bot:MinScoreToNotify");
        AddIfSet(values, "BOT_RISK_PERCENT", "Bot:RiskPercent");
        AddIfSet(values, "BOT_RETAIN_CANDLES_DAYS", "Bot:RetainCandlesDays");
        AddIfSet(values, "BOT_RETAIN_SIGNALS_DAYS", "Bot:RetainSignalsDays");

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

    private static void AddArrayIfSet(IDictionary<string, string?> values, string environmentName, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var items = value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        for (var index = 0; index < items.Length; index++)
        {
            values[$"{configurationKey}:{index}"] = items[index];
        }
    }
}
