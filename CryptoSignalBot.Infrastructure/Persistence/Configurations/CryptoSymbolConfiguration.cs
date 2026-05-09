using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoSignalBot.Infrastructure.Persistence.Configurations;

public sealed class CryptoSymbolConfiguration : IEntityTypeConfiguration<CryptoSymbolEntity>
{
    public void Configure(EntityTypeBuilder<CryptoSymbolEntity> builder)
    {
        builder.ToTable("CryptoSymbols");

        builder.HasKey(symbol => symbol.Id);

        builder.Property(symbol => symbol.Symbol)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(symbol => symbol.Symbol)
            .IsUnique();

        builder.Property(symbol => symbol.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(symbol => symbol.MinScoreToNotify)
            .HasPrecision(4, 2)
            .HasDefaultValue(7.50m)
            .IsRequired();
    }
}
