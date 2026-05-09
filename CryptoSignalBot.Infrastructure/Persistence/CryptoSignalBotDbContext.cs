using CryptoSignalBot.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoSignalBot.Infrastructure.Persistence;

public sealed class CryptoSignalBotDbContext(DbContextOptions<CryptoSignalBotDbContext> options) : DbContext(options)
{
    public DbSet<CryptoSymbolEntity> CryptoSymbols => Set<CryptoSymbolEntity>();

    public DbSet<MarketCandleEntity> MarketCandles => Set<MarketCandleEntity>();

    public DbSet<SignalEntity> Signals => Set<SignalEntity>();

    public DbSet<SignalRuleResultEntity> SignalRuleResults => Set<SignalRuleResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CryptoSignalBotDbContext).Assembly);
    }
}
