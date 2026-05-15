using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoSignalBot.Infrastructure.Persistence.Configurations;

public sealed class PaperTradeConfiguration : IEntityTypeConfiguration<PaperTradeEntity>
{
    public void Configure(EntityTypeBuilder<PaperTradeEntity> builder)
    {
        builder.ToTable("PaperTrades");
        builder.HasKey(trade => trade.Id);
        builder.HasIndex(trade => trade.SignalId).IsUnique();
        builder.HasIndex(trade => trade.EntryTime);
        builder.HasIndex(trade => trade.Outcome);

        builder.Property(trade => trade.Symbol).HasMaxLength(30).IsRequired();
        builder.Property(trade => trade.Timeframe).HasMaxLength(10).IsRequired();
        builder.Property(trade => trade.EntryPrice).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.Units).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.Invested).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.RemainingUnits).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.CashBefore).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.CashAfter).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.EntryFee).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.ExitFee).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.SlippageCost).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.ExitPrice).HasPrecision(28, 10);
        builder.Property(trade => trade.CurrentPrice).HasPrecision(28, 10).IsRequired();
        builder.Property(trade => trade.BreakEvenStop).HasPrecision(28, 10);
        builder.Property(trade => trade.Outcome).HasMaxLength(30).IsRequired();
    }
}
