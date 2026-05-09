using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoSignalBot.Infrastructure.Persistence.Configurations;

public sealed class SignalConfiguration : IEntityTypeConfiguration<SignalEntity>
{
    public void Configure(EntityTypeBuilder<SignalEntity> builder)
    {
        builder.ToTable("Signals");

        builder.HasKey(signal => signal.Id);

        builder.HasIndex(signal => signal.CreatedAt);

        builder.HasIndex(signal => new { signal.Symbol, signal.Timeframe, signal.SignalType, signal.CreatedAt });

        builder.Property(signal => signal.Symbol)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(signal => signal.Timeframe)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(signal => signal.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(signal => signal.Price).HasPrecision(28, 10).IsRequired();
        builder.Property(signal => signal.Score).HasPrecision(4, 2).IsRequired();

        builder.Property(signal => signal.SignalType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(signal => signal.StopLoss).HasPrecision(28, 10);
        builder.Property(signal => signal.TakeProfit1).HasPrecision(28, 10);
        builder.Property(signal => signal.TakeProfit2).HasPrecision(28, 10);
        builder.Property(signal => signal.RiskReward).HasPrecision(8, 3);

        builder.Property(signal => signal.Summary)
            .IsRequired();
    }
}
