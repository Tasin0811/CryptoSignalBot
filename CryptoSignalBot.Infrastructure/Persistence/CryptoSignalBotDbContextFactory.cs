using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CryptoSignalBot.Infrastructure.Persistence;

public sealed class CryptoSignalBotDbContextFactory : IDesignTimeDbContextFactory<CryptoSignalBotDbContext>
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=CryptoSignalBot;Trusted_Connection=True;TrustServerCertificate=True;";

    public CryptoSignalBotDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        var options = new DbContextOptionsBuilder<CryptoSignalBotDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CryptoSignalBotDbContext(options);
    }

    private static string ResolveConnectionString(string[] args)
    {
        var connectionArgIndex = Array.FindIndex(
            args,
            static arg => string.Equals(arg, "--connection", StringComparison.OrdinalIgnoreCase));

        if (connectionArgIndex >= 0 && connectionArgIndex + 1 < args.Length)
        {
            return args[connectionArgIndex + 1];
        }

        return Environment.GetEnvironmentVariable("ConnectionStrings__CryptoSignalBot") ??
               Environment.GetEnvironmentVariable("CRYPTO_SIGNAL_BOT_CONNECTION_STRING") ??
               DefaultConnectionString;
    }
}
