using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Configuration;
using CryptoSignalBot.Domain.Market;
using CryptoSignalBot.Domain.PaperTrading;
using CryptoSignalBot.Domain.Signals;
using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoSignalBot.Infrastructure.Persistence;

public sealed class EfPersistenceService(CryptoSignalBotDbContext dbContext, IOptions<BotSettings> botSettings) : IPersistenceService
{
    private bool databaseReady;

    public async Task SaveCandlesAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        if (candles.Count == 0)
        {
            return;
        }

        var symbol = candles[0].Symbol;
        var timeframe = candles[0].Timeframe;
        var openTimes = candles.Select(candle => candle.OpenTime).ToArray();
        var existingOpenTimes = await dbContext.MarketCandles
            .Where(candle => candle.Symbol == symbol && candle.Timeframe == timeframe && openTimes.Contains(candle.OpenTime))
            .Select(candle => candle.OpenTime)
            .ToListAsync(cancellationToken);
        var existing = existingOpenTimes.ToHashSet();

        foreach (var candle in candles)
        {
            if (existing.Contains(candle.OpenTime))
            {
                continue;
            }

            dbContext.MarketCandles.Add(new MarketCandleEntity
            {
                Symbol = candle.Symbol,
                Timeframe = candle.Timeframe,
                OpenTime = candle.OpenTime,
                OpenPrice = candle.OpenPrice,
                HighPrice = candle.HighPrice,
                LowPrice = candle.LowPrice,
                ClosePrice = candle.ClosePrice,
                Volume = candle.Volume
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSignalAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        if (botSettings.Value.SignalDedupeMinutes > 0)
        {
            var since = signal.CreatedAt.UtcDateTime.AddMinutes(-botSettings.Value.SignalDedupeMinutes);
            var exists = await dbContext.Signals.AnyAsync(
                existing => existing.Symbol == signal.Symbol &&
                            existing.Timeframe == signal.Timeframe &&
                            existing.SignalType == signal.SignalType.ToString() &&
                            existing.CreatedAt >= since,
                cancellationToken);

            if (exists)
            {
                return;
            }
        }

        var entity = new SignalEntity
        {
            Symbol = signal.Symbol,
            Timeframe = signal.Timeframe,
            CreatedAt = signal.CreatedAt.UtcDateTime,
            Price = signal.Price,
            Score = signal.Score,
            SignalType = signal.SignalType.ToString(),
            StopLoss = signal.StopLoss,
            TakeProfit1 = signal.TakeProfit1,
            TakeProfit2 = signal.TakeProfit2,
            RiskReward = signal.RiskReward,
            Summary = signal.Summary
        };

        foreach (var rule in signal.RuleResults)
        {
            entity.RuleResults.Add(new SignalRuleResultEntity
            {
                RuleName = rule.RuleName,
                ScoreImpact = rule.ScoreImpact,
                Result = rule.Result.ToString(),
                Details = rule.Details
            });
        }

        dbContext.Signals.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string symbol,
        string timeframe,
        int maxCandles,
        CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var candles = await dbContext.MarketCandles
            .AsNoTracking()
            .Where(candle => candle.Symbol == symbol && candle.Timeframe == timeframe)
            .OrderByDescending(candle => candle.OpenTime)
            .Take(Math.Max(1, maxCandles))
            .ToListAsync(cancellationToken);

        return candles
            .OrderBy(candle => candle.OpenTime)
            .Select(MapCandle)
            .ToArray();
    }

    public async Task<IReadOnlyList<Signal>> GetSignalsSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var sinceUtc = since.UtcDateTime;
        var entities = await dbContext.Signals
            .AsNoTracking()
            .Where(signal => signal.CreatedAt >= sinceUtc)
            .OrderByDescending(signal => signal.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapSignal).ToArray();
    }

    public async Task<PaperTradeReport> BuildPaperTradeReportAsync(
        int maxSignals,
        int maxFutureCandles,
        CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var signals = await dbContext.Signals
            .AsNoTracking()
            .Where(signal => signal.Score >= 7.5m && signal.StopLoss != null && signal.TakeProfit1 != null)
            .OrderByDescending(signal => signal.CreatedAt)
            .Take(maxSignals)
            .ToListAsync(cancellationToken);

        var results = new List<PaperTradeResult>();
        foreach (var signal in signals)
        {
            var futureCandles = await dbContext.MarketCandles
                .AsNoTracking()
                .Where(candle => candle.Symbol == signal.Symbol &&
                                 candle.Timeframe == signal.Timeframe &&
                                 candle.OpenTime > signal.CreatedAt)
                .OrderBy(candle => candle.OpenTime)
                .Take(maxFutureCandles)
                .ToListAsync(cancellationToken);

            results.Add(EvaluatePaperTrade(signal, futureCandles, maxFutureCandles));
        }

        return new PaperTradeReport(DateTimeOffset.UtcNow, results);
    }

    public async Task<PaperPortfolioReport> BuildPaperPortfolioReportAsync(
        decimal initialBudget,
        int maxSignals,
        int maxFutureCandles,
        CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var startingBudget = initialBudget > 0m ? initialBudget : botSettings.Value.PaperPortfolioInitialBudget;
        var cash = startingBudget;
        var openPositionValue = 0m;
        var availableAt = DateTime.MinValue;
        var trades = new List<PaperPortfolioTrade>();
        var signals = await dbContext.Signals
            .AsNoTracking()
            .Where(signal => signal.Score >= botSettings.Value.MinScoreToNotify &&
                             signal.StopLoss != null &&
                             signal.TakeProfit1 != null)
            .OrderByDescending(signal => signal.CreatedAt)
            .Take(Math.Max(1, maxSignals))
            .OrderBy(signal => signal.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var signal in signals)
        {
            if (signal.CreatedAt <= availableAt || cash <= 0m)
            {
                continue;
            }

            var futureCandles = await dbContext.MarketCandles
                .AsNoTracking()
                .Where(candle => candle.Symbol == signal.Symbol &&
                                 candle.Timeframe == signal.Timeframe &&
                                 candle.OpenTime > signal.CreatedAt)
                .OrderBy(candle => candle.OpenTime)
                .Take(Math.Max(1, maxFutureCandles))
                .ToListAsync(cancellationToken);

            var trade = CreatePortfolioTrade(signal, futureCandles, cash, botSettings.Value.RiskPercent);
            if (trade is null)
            {
                continue;
            }

            cash = trade.CashAfter;
            if (trade.IsClosed)
            {
                availableAt = trade.ExitTime ?? signal.CreatedAt;
            }
            else
            {
                openPositionValue += trade.CurrentValue;
                availableAt = DateTime.MaxValue;
            }

            trades.Add(trade);
        }

        return new PaperPortfolioReport(DateTimeOffset.UtcNow, startingBudget, cash, openPositionValue, trades);
    }

    public async Task<int> CleanupDatabaseAsync(
        DateTimeOffset retainCandlesSince,
        DateTimeOffset retainSignalsSince,
        CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var candlesSinceUtc = retainCandlesSince.UtcDateTime;
        var signalsSinceUtc = retainSignalsSince.UtcDateTime;

        var oldCandles = await dbContext.MarketCandles
            .Where(candle => candle.OpenTime < candlesSinceUtc)
            .ExecuteDeleteAsync(cancellationToken);

        var oldSignals = await dbContext.Signals
            .Where(signal => signal.CreatedAt < signalsSinceUtc)
            .ExecuteDeleteAsync(cancellationToken);

        return oldCandles + oldSignals;
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        if (databaseReady)
        {
            return;
        }

        if (dbContext.Database.IsSqlServer())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);

            await EnsureSqlServerIndexAsync(
                "IX_Signals_CreatedAt",
                "Signals",
                "CREATE INDEX [IX_Signals_CreatedAt] ON [Signals] ([CreatedAt]);",
                cancellationToken);
            await EnsureSqlServerIndexAsync(
                "IX_Signals_Symbol_Timeframe_SignalType_CreatedAt",
                "Signals",
                "CREATE INDEX [IX_Signals_Symbol_Timeframe_SignalType_CreatedAt] ON [Signals] ([Symbol], [Timeframe], [SignalType], [CreatedAt]);",
                cancellationToken);
            await EnsureSqlServerIndexAsync(
                "IX_MarketCandles_OpenTime",
                "MarketCandles",
                "CREATE INDEX [IX_MarketCandles_OpenTime] ON [MarketCandles] ([OpenTime]);",
                cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        databaseReady = true;
    }

    private async Task EnsureSqlServerIndexAsync(
        string indexName,
        string tableName,
        string createIndexSql,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            IF OBJECT_ID(N'[{tableName}]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'{indexName}'
                     AND object_id = OBJECT_ID(N'[{tableName}]'))
            BEGIN
                {createIndexSql}
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static Signal MapSignal(SignalEntity entity)
    {
        return new Signal(
            entity.Symbol,
            entity.Timeframe,
            new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            entity.Price,
            entity.Score,
            Enum.TryParse<SignalType>(entity.SignalType, out var signalType) ? signalType : SignalType.Avoid,
            RiskLevel.Medium,
            entity.StopLoss,
            entity.TakeProfit1,
            entity.TakeProfit2,
            entity.RiskReward,
            entity.Summary,
            []);
    }

    private static Candle MapCandle(MarketCandleEntity entity)
    {
        return new Candle(
            entity.Symbol,
            entity.Timeframe,
            entity.OpenTime,
            entity.OpenTime,
            entity.OpenPrice,
            entity.HighPrice,
            entity.LowPrice,
            entity.ClosePrice,
            entity.Volume);
    }

    private static PaperTradeResult EvaluatePaperTrade(
        SignalEntity signal,
        IReadOnlyList<MarketCandleEntity> futureCandles,
        int maxFutureCandles)
    {
        if (!signal.StopLoss.HasValue || !signal.TakeProfit1.HasValue || signal.Price <= 0)
        {
            return CreatePaperResult(signal, PaperTradeOutcome.Invalid, null, null);
        }

        foreach (var candle in futureCandles)
        {
            if (candle.LowPrice <= signal.StopLoss.Value)
            {
                return CreatePaperResult(signal, PaperTradeOutcome.StopLoss, candle.OpenTime, signal.StopLoss.Value);
            }

            if (candle.HighPrice >= signal.TakeProfit1.Value)
            {
                return CreatePaperResult(signal, PaperTradeOutcome.TakeProfit1, candle.OpenTime, signal.TakeProfit1.Value);
            }
        }

        return futureCandles.Count < maxFutureCandles
            ? CreatePaperResult(signal, PaperTradeOutcome.Open, null, null)
            : CreatePaperResult(signal, PaperTradeOutcome.Expired, futureCandles[^1].OpenTime, futureCandles[^1].ClosePrice);
    }

    private static PaperPortfolioTrade? CreatePortfolioTrade(
        SignalEntity signal,
        IReadOnlyList<MarketCandleEntity> futureCandles,
        decimal cash,
        decimal riskPercent)
    {
        if (signal.Price <= 0m || signal.StopLoss is null || signal.TakeProfit1 is null || signal.StopLoss >= signal.Price)
        {
            return null;
        }

        var stopDistance = signal.Price - signal.StopLoss.Value;
        var riskAmount = cash * Math.Clamp(riskPercent, 0.001m, 0.20m);
        var riskBasedUnits = riskAmount / stopDistance;
        var cashBasedUnits = cash / signal.Price;
        var units = Math.Min(riskBasedUnits, cashBasedUnits);
        if (units <= 0m)
        {
            return null;
        }

        var invested = units * signal.Price;
        var cashBefore = cash;
        var cashAfterEntry = cashBefore - invested;
        PaperTradeOutcome outcome;
        DateTime? exitTime = null;
        decimal? exitPrice = null;
        var currentPrice = signal.Price;

        foreach (var candle in futureCandles)
        {
            currentPrice = candle.ClosePrice;
            if (candle.LowPrice <= signal.StopLoss.Value)
            {
                outcome = PaperTradeOutcome.StopLoss;
                exitTime = candle.OpenTime;
                exitPrice = signal.StopLoss.Value;
                currentPrice = exitPrice.Value;
                return CreatePortfolioTrade(signal, units, invested, cashBefore, cashAfterEntry + (units * currentPrice), exitTime, exitPrice, currentPrice, outcome);
            }

            if (candle.HighPrice >= signal.TakeProfit1.Value)
            {
                outcome = PaperTradeOutcome.TakeProfit1;
                exitTime = candle.OpenTime;
                exitPrice = signal.TakeProfit1.Value;
                currentPrice = exitPrice.Value;
                return CreatePortfolioTrade(signal, units, invested, cashBefore, cashAfterEntry + (units * currentPrice), exitTime, exitPrice, currentPrice, outcome);
            }
        }

        if (futureCandles.Count == 0)
        {
            return CreatePortfolioTrade(signal, units, invested, cashBefore, cashAfterEntry, null, null, currentPrice, PaperTradeOutcome.Open);
        }

        var lastCandle = futureCandles[^1];
        return CreatePortfolioTrade(
            signal,
            units,
            invested,
            cashBefore,
            cashAfterEntry + (units * lastCandle.ClosePrice),
            lastCandle.OpenTime,
            lastCandle.ClosePrice,
            lastCandle.ClosePrice,
            PaperTradeOutcome.Expired);
    }

    private static PaperPortfolioTrade CreatePortfolioTrade(
        SignalEntity signal,
        decimal units,
        decimal invested,
        decimal cashBefore,
        decimal cashAfter,
        DateTime? exitTime,
        decimal? exitPrice,
        decimal currentPrice,
        PaperTradeOutcome outcome)
    {
        return new PaperPortfolioTrade(
            signal.Id,
            signal.Symbol,
            signal.Timeframe,
            signal.CreatedAt,
            signal.Price,
            units,
            invested,
            cashBefore,
            cashAfter,
            exitTime,
            exitPrice,
            currentPrice,
            outcome);
    }

    private static PaperTradeResult CreatePaperResult(
        SignalEntity signal,
        PaperTradeOutcome outcome,
        DateTime? exitTime,
        decimal? exitPrice)
    {
        decimal? returnPercent = exitPrice.HasValue
            ? decimal.Round((exitPrice.Value - signal.Price) / signal.Price * 100m, 4)
            : null;

        return new PaperTradeResult(
            signal.Id,
            signal.Symbol,
            signal.Timeframe,
            signal.CreatedAt,
            signal.Price,
            signal.StopLoss,
            signal.TakeProfit1,
            signal.Score,
            signal.SignalType,
            outcome,
            exitTime,
            exitPrice,
            returnPercent);
    }
}
