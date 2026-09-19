using System.IO;
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
/// WPF 应用程序入口
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 服务提供容器
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ServiceProvider = ConfigureServices();

        // 确保数据库已创建
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
        context.Database.EnsureCreated();

        // 初始化账户
        var settings = scope.ServiceProvider.GetRequiredService<ServiceSettings>();
        var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();
        accountManager.InitializeAsync(settings.InitialCapital).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 配置依赖注入服务
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var settings = LoadSettings();
        services.AddSingleton(settings);

        // 数据库上下文工厂（WPF 后台线程与 UI 线程安全共享，便携目录）
        services.AddDbContextFactory<TradingDbContext>(options =>
        {
            options.UseSqlite($"Data Source={settings.ResolveDatabasePath()}");
        });

        // 日志（便携目录）
        services.AddSingleton<ILoggerService>(provider =>
        {
            var factory = provider.GetRequiredService<IDbContextFactory<TradingDbContext>>();
            using var context = factory.CreateDbContext();
            return new SerilogLoggerService(settings.ResolveLogDirectory(), context);
        });

        // 数据仓储
        services.AddScoped<IStockRepository, EfStockRepository>();
        services.AddScoped<IOrderManager, EfOrderManager>();
        services.AddScoped<IPositionManager, EfPositionManager>();
        services.AddScoped<IAccountManager, EfAccountManager>();
        services.AddScoped<ITradeQueryService, EfTradeQueryService>();

        // 行情源与交易执行器
        services.AddSingleton<IMarketDataProvider>(_ => MarketDataProviderFactory.Create(settings));
        services.AddSingleton<ITradeExecutor>(provider =>
        {
            var factory = provider.GetRequiredService<IDbContextFactory<TradingDbContext>>();
            var logger = provider.GetRequiredService<ILoggerService>();
            var notification = provider.GetRequiredService<INotificationService>();
            return TradeExecutorFactory.Create(settings, factory, logger, notification);
        });

        // 通知服务
        services.AddSingleton<INotificationService, WindowsNotificationService>();

        // 策略引擎与交易服务
        services.AddSingleton<IStrategyEngine, ThresholdStrategyEngine>();
        services.AddSingleton<ITradingService, TradingService>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 加载应用设置（优先从程序目录的 appsettings.json 读取，便携版）
    /// </summary>
    private static ServiceSettings LoadSettings()
    {
        // 优先使用便携目录下的 appsettings.json（与 exe 同级）
        var configPath = StockAutoTrader.Core.PortablePathHelper.GetConfigPath();
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<ServiceSettings>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
            catch
            {
                // 解析失败时回退默认设置
            }
        }
        return new ServiceSettings();
    }
}
