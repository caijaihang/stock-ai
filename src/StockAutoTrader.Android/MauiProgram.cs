using Microsoft.Extensions.DependencyInjection;
using StockAutoTrader.Android.Services;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Strategies;
using StockAutoTrader.Infrastructure.Providers;

namespace StockAutoTrader.Android;

/// <summary>
/// MAUI Android 应用入口
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 创建 MAUI 应用
    /// </summary>
    public static MauiApp CreateMauiApp()
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
