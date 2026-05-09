using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoSignalBot.Infrastructure.Persistence.Configurations;

public sealed class SignalRuleResultConfiguration : IEntityTypeConfiguration<SignalRuleResultEntity>
{
    public void Configure(EntityTypeBuilder<SignalRuleResultEntity> builder)
    {
        builder.ToTable("SignalRuleResults");

        builder.HasKey(result => result.Id);

        builder.Property(result => result.RuleName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(result => result.ScoreImpact)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(result => result.Result)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne(result => result.Signal)
            .WithMany(signal => signal.RuleResults)
            .HasForeignKey(result => result.SignalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
