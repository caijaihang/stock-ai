using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 行情源工厂
/// </summary>
public static class MarketDataProviderFactory
{
    /// <summary>
    /// 创建行情源实例
    /// </summary>
    public static IMarketDataProvider Create(ServiceSettings settings)
    {
        return settings.MarketDataProvider.ToLowerInvariant() switch
        {
            "tongdaxin" => new TongDaXinMarketDataProvider(settings.TongDaXinExportDirectory),
            "tonghuashun" => new TongHuaShunMarketDataProvider(settings.TongHuaShunExportDirectory),
            _ => new SimulatedMarketDataProvider(0.005)
        };
    }
}
