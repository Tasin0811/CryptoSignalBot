using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoSignalBot.Infrastructure.Persistence.Migrations;

public partial class InitialSqlServerLocalDb : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[CryptoSymbols]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CryptoSymbols] (
                    [Id] int NOT NULL IDENTITY,
                    [Symbol] nvarchar(30) NOT NULL,
                    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
                    [MinScoreToNotify] decimal(4,2) NOT NULL DEFAULT 7.50,
                    CONSTRAINT [PK_CryptoSymbols] PRIMARY KEY ([Id])
                );
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[MarketCandles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MarketCandles] (
                    [Id] bigint NOT NULL IDENTITY,
                    [Symbol] nvarchar(30) NOT NULL,
                    [Timeframe] nvarchar(10) NOT NULL,
                    [OpenTime] datetime2 NOT NULL,
                    [OpenPrice] decimal(28,10) NOT NULL,
                    [HighPrice] decimal(28,10) NOT NULL,
                    [LowPrice] decimal(28,10) NOT NULL,
                    [ClosePrice] decimal(28,10) NOT NULL,
                    [Volume] decimal(28,10) NOT NULL,
                    CONSTRAINT [PK_MarketCandles] PRIMARY KEY ([Id])
                );
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[Signals]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Signals] (
                    [Id] bigint NOT NULL IDENTITY,
                    [Symbol] nvarchar(30) NOT NULL,
                    [Timeframe] nvarchar(10) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
                    [Price] decimal(28,10) NOT NULL,
                    [Score] decimal(4,2) NOT NULL,
                    [SignalType] nvarchar(20) NOT NULL,
                    [StopLoss] decimal(28,10) NULL,
                    [TakeProfit1] decimal(28,10) NULL,
                    [TakeProfit2] decimal(28,10) NULL,
                    [RiskReward] decimal(8,3) NULL,
                    [Summary] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_Signals] PRIMARY KEY ([Id])
                );
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[SignalRuleResults]', N'U') IS NULL
            BEGIN
                CREATE TABLE [SignalRuleResults] (
                    [Id] bigint NOT NULL IDENTITY,
                    [SignalId] bigint NOT NULL,
                    [RuleName] nvarchar(100) NOT NULL,
                    [ScoreImpact] decimal(5,2) NOT NULL,
                    [Result] nvarchar(30) NOT NULL,
                    [Details] nvarchar(max) NULL,
                    CONSTRAINT [PK_SignalRuleResults] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SignalRuleResults_Signals_SignalId] FOREIGN KEY ([SignalId]) REFERENCES [Signals] ([Id]) ON DELETE CASCADE
                );
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[CryptoSymbols]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'IX_CryptoSymbols_Symbol'
                     AND object_id = OBJECT_ID(N'[CryptoSymbols]'))
            BEGIN
                CREATE UNIQUE INDEX [IX_CryptoSymbols_Symbol] ON [CryptoSymbols] ([Symbol]);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[MarketCandles]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'UQ_MarketCandles'
                     AND object_id = OBJECT_ID(N'[MarketCandles]'))
            BEGIN
                CREATE UNIQUE INDEX [UQ_MarketCandles] ON [MarketCandles] ([Symbol], [Timeframe], [OpenTime]);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[MarketCandles]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'IX_MarketCandles_OpenTime'
                     AND object_id = OBJECT_ID(N'[MarketCandles]'))
            BEGIN
                CREATE INDEX [IX_MarketCandles_OpenTime] ON [MarketCandles] ([OpenTime]);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[SignalRuleResults]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'IX_SignalRuleResults_SignalId'
                     AND object_id = OBJECT_ID(N'[SignalRuleResults]'))
            BEGIN
                CREATE INDEX [IX_SignalRuleResults_SignalId] ON [SignalRuleResults] ([SignalId]);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[Signals]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'IX_Signals_CreatedAt'
                     AND object_id = OBJECT_ID(N'[Signals]'))
            BEGIN
                CREATE INDEX [IX_Signals_CreatedAt] ON [Signals] ([CreatedAt]);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[Signals]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = N'IX_Signals_Symbol_Timeframe_SignalType_CreatedAt'
                     AND object_id = OBJECT_ID(N'[Signals]'))
            BEGIN
                CREATE INDEX [IX_Signals_Symbol_Timeframe_SignalType_CreatedAt] ON [Signals] ([Symbol], [Timeframe], [SignalType], [CreatedAt]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[SignalRuleResults]', N'U') IS NOT NULL
                DROP TABLE [SignalRuleResults];

            IF OBJECT_ID(N'[Signals]', N'U') IS NOT NULL
                DROP TABLE [Signals];

            IF OBJECT_ID(N'[MarketCandles]', N'U') IS NOT NULL
                DROP TABLE [MarketCandles];

            IF OBJECT_ID(N'[CryptoSymbols]', N'U') IS NOT NULL
                DROP TABLE [CryptoSymbols];
            """);
    }
}
