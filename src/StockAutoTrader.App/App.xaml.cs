using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockAutoTrader.App.Services;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Strategies;
using StockAutoTrader.Infrastructure.Data;
using StockAutoTrader.Infrastructure.Logging;
using StockAutoTrader.Infrastructure.Providers;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.App;

/// <summary>
/// WPF 应用程序入口（单实例 + 崩溃恢复自动启动）
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "Global\\StockAutoTrader_SingleInstance";

    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例互斥锁，防止多开导致重复交易
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("StockAutoTrader 已在运行中，请勿重复启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ServiceProvider = ConfigureServices();

        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
        context.Database.EnsureCreated();

        var settings = scope.ServiceProvider.GetRequiredService<ServiceSettings>();
        var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();
        accountManager.InitializeAsync(settings.InitialCapital).GetAwaiter().GetResult();

        // 崩溃恢复：若上次有运行中的股票，自动启动监控
        var stockRepo = scope.ServiceProvider.GetRequiredService<IStockRepository>();
        var configs = stockRepo.GetAllConfigsAsync().GetAwaiter().GetResult();
        if (configs.Any(c => c.Status == Core.Enums.StockStatus.Running))
        {
            var tradingService = scope.ServiceProvider.GetRequiredService<ITradingService>();
            tradingService.StartAsync().GetAwaiter().GetResult();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        var settings = LoadSettings();
        services.AddSingleton(settings);

        services.AddDbContextFactory<TradingDbContext>(options =>
            options.UseSqlite($"Data Source={settings.ResolveDatabasePath()}"));

        services.AddSingleton<ILoggerService>(provider =>
        {
            var factory = provider.GetRequiredService<IDbContextFactory<TradingDbContext>>();
            using var ctx = factory.CreateDbContext();
            return new SerilogLoggerService(settings.ResolveLogDirectory(), ctx);
        });

        services.AddScoped<IStockRepository, EfStockRepository>();
        services.AddScoped<IOrderManager, EfOrderManager>();
        services.AddScoped<IPositionManager, EfPositionManager>();
        services.AddScoped<IAccountManager, EfAccountManager>();
        services.AddScoped<ITradeQueryService, EfTradeQueryService>();

        services.AddSingleton<IMarketDataProvider>(_ => MarketDataProviderFactory.Create(settings));
        services.AddSingleton<ITradeExecutor>(provider =>
        {
            var factory = provider.GetRequiredService<IDbContextFactory<TradingDbContext>>();
            var logger = provider.GetRequiredService<ILoggerService>();
            var notification = provider.GetRequiredService<INotificationService>();
            return TradeExecutorFactory.Create(settings, factory, logger, notification);
        });

        services.AddSingleton<INotificationService, WindowsNotificationService>();
        services.AddSingleton<IStrategyEngine, ThresholdStrategyEngine>();
        services.AddSingleton<AiStockSelector>();
        services.AddSingleton<ExternalTradingAppLauncher>();
        services.AddSingleton<ITradingService>(provider =>
        {
            var market = provider.GetRequiredService<IMarketDataProvider>();
            var trade = provider.GetRequiredService<ITradeExecutor>();
            var strategy = provider.GetRequiredService<IStrategyEngine>();
            var stockRepo = provider.GetRequiredService<IStockRepository>();
            var orderMgr = provider.GetRequiredService<IOrderManager>();
            var posMgr = provider.GetRequiredService<IPositionManager>();
            var accountMgr = provider.GetRequiredService<IAccountManager>();
            var tradeQuery = provider.GetRequiredService<ITradeQueryService>();
            var logger = provider.GetRequiredService<ILoggerService>();
            var notification = provider.GetRequiredService<INotificationService>();
            return new TradingService(market, trade, strategy, stockRepo, orderMgr, posMgr, accountMgr, tradeQuery, logger, notification, settings.RefreshIntervalMs);
        });

        return services.BuildServiceProvider();
    }

    private static ServiceSettings LoadSettings()
    {
        var configPath = StockAutoTrader.Core.PortablePathHelper.GetConfigPath();
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<ServiceSettings>(json);
                if (loaded != null) return loaded;
            }
            catch { /* 解析失败回退默认 */ }
        }
        return new ServiceSettings();
    }
}
