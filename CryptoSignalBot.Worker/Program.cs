using CryptoSignalBot.Application;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Infrastructure;
using CryptoSignalBot.Infrastructure.Configuration;
using CryptoSignalBot.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
var runtimeSettingsPath = Environment.GetEnvironmentVariable("CRYPTO_SIGNAL_BOT_RUNTIME_SETTINGS");
if (!string.IsNullOrWhiteSpace(runtimeSettingsPath))
{
    builder.Configuration.AddJsonFile(runtimeSettingsPath, optional: true, reloadOnChange: true);
}

builder.Configuration.AddCryptoSignalBotEnvironmentVariables();

var logRoot = Environment.GetEnvironmentVariable("CRYPTO_SIGNAL_BOT_LOG_DIR");
if (string.IsNullOrWhiteSpace(logRoot))
{
    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    logRoot = string.IsNullOrWhiteSpace(programData)
        ? Path.Combine(AppContext.BaseDirectory, "logs")
        : Path.Combine(programData, "CryptoSignalBot", "logs");
}

Directory.CreateDirectory(logRoot);
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logRoot, "worker-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("Bot"));
builder.Services.Configure<BinanceSettings>(builder.Configuration.GetSection("Binance"));
builder.Services.Configure<CoinGeckoSettings>(builder.Configuration.GetSection("CoinGecko"));
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
try
{
    host.Run();
}
finally
{
    Log.CloseAndFlush();
}
