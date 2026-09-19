using StockAutoTrader.Android.Services;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Strategies;
using StockAutoTrader.Infrastructure.Providers;

namespace StockAutoTrader.Android;

/// <summary>
/// MAUI Android 应用类
/// 基类 MauiApplication 由框架提供，重写 CreateMauiApp 注册 DI 服务
/// </summary>
public partial class App : MauiApplication
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }

    /// <summary>
    /// 创建 MAUI 应用并注册 Android 端服务
    /// </summary>
    protected override MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // 日志与通知
        builder.Services.AddSingleton<ILoggerService, NoopLoggerService>();
        builder.Services.AddSingleton<INotificationService, AndroidNotificationService>();

        // 行情源（Android 端默认模拟，后续可接同一套文件桥）
        builder.Services.AddSingleton<IMarketDataProvider>(_ => new SimulatedMarketDataProvider(0.005));

        // 策略引擎
        builder.Services.AddSingleton<IStrategyEngine, ThresholdStrategyEngine>();

        return builder.Build();
    }
}
