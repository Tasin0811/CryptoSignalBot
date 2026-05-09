using CryptoSignalBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace CryptoSignalBot.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CryptoSignalBotDbContext))]
partial class CryptoSignalBotDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.10")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("CryptoSignalBot.Infrastructure.Persistence.Entities.CryptoSymbolEntity", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("int");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

            b.Property<bool>("IsActive")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(true);

            b.Property<decimal>("MinScoreToNotify")
                .ValueGeneratedOnAdd()
                .HasPrecision(4, 2)
                .HasColumnType("decimal(4,2)")
                .HasDefaultValue(7.50m);

            b.Property<string>("Symbol")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("nvarchar(30)");

            b.HasKey("Id");

            b.HasIndex("Symbol")
                .IsUnique();

            b.ToTable("CryptoSymbols", (string)null);
        });

        modelBuilder.Entity("CryptoSignalBot.Infrastructure.Persistence.Entities.MarketCandleEntity", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

            b.Property<decimal>("ClosePrice")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<decimal>("HighPrice")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<decimal>("LowPrice")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<DateTime>("OpenTime")
                .HasColumnType("datetime2");

            b.Property<decimal>("OpenPrice")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<string>("Symbol")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("nvarchar(30)");

            b.Property<string>("Timeframe")
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnType("nvarchar(10)");

            b.Property<decimal>("Volume")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.HasKey("Id");

            b.HasIndex("OpenTime");

            b.HasIndex("Symbol", "Timeframe", "OpenTime")
                .IsUnique()
                .HasDatabaseName("UQ_MarketCandles");

            b.ToTable("MarketCandles", (string)null);
        });

        modelBuilder.Entity("CryptoSignalBot.Infrastructure.Persistence.Entities.SignalEntity", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<decimal>("Price")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<decimal?>("RiskReward")
                .HasPrecision(8, 3)
                .HasColumnType("decimal(8,3)");

            b.Property<decimal>("Score")
                .HasPrecision(4, 2)
                .HasColumnType("decimal(4,2)");

            b.Property<string>("SignalType")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("nvarchar(20)");

            b.Property<decimal?>("StopLoss")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<string>("Summary")
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            b.Property<string>("Symbol")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("nvarchar(30)");

            b.Property<decimal?>("TakeProfit1")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<decimal?>("TakeProfit2")
                .HasPrecision(28, 10)
                .HasColumnType("decimal(28,10)");

            b.Property<string>("Timeframe")
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnType("nvarchar(10)");

            b.HasKey("Id");

            b.HasIndex("CreatedAt");

            b.HasIndex("Symbol", "Timeframe", "SignalType", "CreatedAt");

            b.ToTable("Signals", (string)null);
        });

        modelBuilder.Entity("CryptoSignalBot.Infrastructure.Persistence.Entities.SignalRuleResultEntity", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

            b.Property<string>("Details")
                .HasColumnType("nvarchar(max)");

            b.Property<string>("Result")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("nvarchar(30)");

            b.Property<string>("RuleName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<decimal>("ScoreImpact")
                .HasPrecision(5, 2)
                .HasColumnType("decimal(5,2)");

            b.Property<long>("SignalId")
                .HasColumnType("bigint");

            b.HasKey("Id");

            b.HasIndex("SignalId");

            b.ToTable("SignalRuleResults", (string)null);
        });

        modelBuilder.Entity("CryptoSignalBot.Infrastructure.Persistence.Entities.SignalRuleResultEntity", b =>
        {
            b.HasOne("CryptoSignalBot.Infrastructure.Persistence.Entities.SignalEntity", "Signal")
                .WithMany("RuleResults")
                .HasForeignKey("SignalId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Signal");
        });

        modelBuilder.Entity("CryptoSignalBot.Infrastructure.Persistence.Entities.SignalEntity", b =>
        {
            b.Navigation("RuleResults");
        });
#pragma warning restore 612, 618
    }
}
