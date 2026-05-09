using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.Signals;
using CryptoSignalBot.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Application.Tests;

public sealed class EfPersistenceServiceTests
{
    [Fact]
    public async Task SaveSignalAsync_SuppressesDuplicateSignalInsideOneHourWindow()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);
        var firstSignal = CreateSignal(createdAt: new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero));
        var duplicateSignal = CreateSignal(createdAt: new DateTimeOffset(2026, 5, 9, 12, 45, 0, TimeSpan.Zero), price: 101m);

        await service.SaveSignalAsync(firstSignal);
        await service.SaveSignalAsync(duplicateSignal);

        var savedSignals = await database.Context.Signals
            .OrderBy(signal => signal.CreatedAt)
            .ToArrayAsync();

        Assert.Single(savedSignals);
        Assert.Equal(firstSignal.Price, savedSignals[0].Price);
    }

    [Fact]
    public async Task SaveSignalAsync_AllowsSameSignalAfterOneHourWindow()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);
        var firstSignal = CreateSignal(createdAt: new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero));
        var freshSignal = CreateSignal(createdAt: new DateTimeOffset(2026, 5, 9, 13, 1, 0, TimeSpan.Zero), price: 101m);

        await service.SaveSignalAsync(firstSignal);
        await service.SaveSignalAsync(freshSignal);

        Assert.Equal(2, await database.Context.Signals.CountAsync());
    }

    [Fact]
    public async Task CleanupDatabaseAsync_RemovesOnlyCandlesAndSignalsOlderThanRetentionCutoffs()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);
        var oldCandleTime = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);
        var retainedCandleTime = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc);
        var oldSignalTime = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        var retainedSignalTime = new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

        await service.SaveCandlesAsync(
        [
            CreateCandle(oldCandleTime),
            CreateCandle(retainedCandleTime)
        ]);
        await service.SaveSignalAsync(CreateSignal(createdAt: oldSignalTime, signalType: SignalType.BuyWatch));
        await service.SaveSignalAsync(CreateSignal(createdAt: retainedSignalTime, signalType: SignalType.HighQualitySetup));

        var deleted = await service.CleanupDatabaseAsync(
            new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, deleted);
        var remainingCandle = Assert.Single(await database.Context.MarketCandles.ToArrayAsync());
        var remainingSignal = Assert.Single(await database.Context.Signals.ToArrayAsync());
        Assert.Equal(retainedCandleTime, remainingCandle.OpenTime);
        Assert.Equal(retainedSignalTime.UtcDateTime, remainingSignal.CreatedAt);
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CryptoSignalBotDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new CryptoSignalBotDbContext(options);

        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(connection, context);
    }

    private static EfPersistenceService CreateService(
        CryptoSignalBotDbContext context,
        int signalDedupeMinutes = 60)
    {
        return new EfPersistenceService(
            context,
            Options.Create(new BotSettings { SignalDedupeMinutes = signalDedupeMinutes }));
    }

    private static Signal CreateSignal(
        DateTimeOffset createdAt,
        SignalType signalType = SignalType.BuyWatch,
        decimal price = 100m)
    {
        return new Signal(
            "BTCUSDT",
            "1h",
            createdAt,
            price,
            8m,
            signalType,
            RiskLevel.Low,
            95m,
            105m,
            110m,
            1.5m,
            "persistence test",
            [new RuleResult("Rule", 1m, RuleResultType.Pass, "details")]);
    }

    private static Candle CreateCandle(DateTime openTime)
    {
        return new Candle(
            "BTCUSDT",
            "1h",
            openTime,
            openTime.AddHours(1).AddSeconds(-1),
            100m,
            101m,
            99m,
            100.5m,
            1000m);
    }

    private sealed class TestDatabase(SqliteConnection connection, CryptoSignalBotDbContext context) : IAsyncDisposable
    {
        public CryptoSignalBotDbContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
