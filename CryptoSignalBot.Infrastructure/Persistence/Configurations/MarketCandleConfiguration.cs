using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoSignalBot.Infrastructure.Persistence.Configurations;

public sealed class MarketCandleConfiguration : IEntityTypeConfiguration<MarketCandleEntity>
{
    public void Configure(EntityTypeBuilder<MarketCandleEntity> builder)
    {
        builder.ToTable("MarketCandles");

        builder.HasKey(candle => candle.Id);

        builder.Property(candle => candle.Symbol)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(candle => candle.Timeframe)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(candle => candle.OpenTime)
            .IsRequired();

        builder.Property(candle => candle.OpenPrice).HasPrecision(28, 10).IsRequired();
        builder.Property(candle => candle.HighPrice).HasPrecision(28, 10).IsRequired();
        builder.Property(candle => candle.LowPrice).HasPrecision(28, 10).IsRequired();
        builder.Property(candle => candle.ClosePrice).HasPrecision(28, 10).IsRequired();
        builder.Property(candle => candle.Volume).HasPrecision(28, 10).IsRequired();

        builder.HasIndex(candle => new { candle.Symbol, candle.Timeframe, candle.OpenTime })
            .IsUnique()
            .HasDatabaseName("UQ_MarketCandles");

        builder.HasIndex(candle => candle.OpenTime);
    }
}
