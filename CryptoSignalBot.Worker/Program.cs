using CryptoSignalBot.Application;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Infrastructure;
using CryptoSignalBot.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("Bot"));
builder.Services.Configure<BinanceSettings>(builder.Configuration.GetSection("Binance"));
builder.Services.Configure<CoinGeckoSettings>(builder.Configuration.GetSection("CoinGecko"));
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
