namespace CryptoSignalBot.Domain.Configuration;

public sealed class TelegramSettings
{
    public string BotToken { get; init; } = string.Empty;
    public string ChatId { get; init; } = string.Empty;
}
