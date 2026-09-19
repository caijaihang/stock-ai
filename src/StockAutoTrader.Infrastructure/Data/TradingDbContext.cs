using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Infrastructure.Data;

/// <summary>
/// 交易数据库上下文
/// </summary>
public class TradingDbContext : DbContext
{
    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string DbPath { get; }

    public TradingDbContext()
    {
        // 便携版：默认数据库写到程序解压目录的 data/ 子目录，不存 C 盘
        var folder = StockAutoTrader.Core.PortablePathHelper.GetDataDirectory();
        Directory.CreateDirectory(folder);
        DbPath = Path.Combine(folder, "stock_auto_trader.db");
    }

    public TradingDbContext(string dbPath)
    {
        DbPath = dbPath;
        var folder = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
    {
        DbPath = "memory";
    }

    public DbSet<StockConfig> StockConfigs { get; set; } = null!;

    public DbSet<Position> Positions { get; set; } = null!;

    public DbSet<Order> Orders { get; set; } = null!;

    public DbSet<Trade> Trades { get; set; } = null!;

    public DbSet<Account> Accounts { get; set; } = null!;

    public DbSet<TradingLog> TradingLogs { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StockCode).IsUnique();
            entity.Property(e => e.StockCode).HasMaxLength(20);
            entity.Property(e => e.StockName).HasMaxLength(50);
            entity.Property(e => e.Market).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.BenchmarkPriceType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StockCode).IsUnique();
            entity.Property(e => e.StockCode).HasMaxLength(20);
            entity.Property(e => e.StockName).HasMaxLength(50);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.StockCode);
            entity.Property(e => e.OrderId).HasMaxLength(64);
            entity.Property(e => e.StockCode).HasMaxLength(20);
            entity.Property(e => e.StockName).HasMaxLength(50);
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.StockCode);
            entity.Property(e => e.TradeId).HasMaxLength(64);
            entity.Property(e => e.OrderId).HasMaxLength(64);
            entity.Property(e => e.StockCode).HasMaxLength(20);
            entity.Property(e => e.StockName).HasMaxLength(50);
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountName).HasMaxLength(50);
        });

        modelBuilder.Entity<TradingLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.StockCode);
            entity.Property(e => e.StockCode).HasMaxLength(20);
            entity.Property(e => e.Level).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(50);
        });
    }
}
