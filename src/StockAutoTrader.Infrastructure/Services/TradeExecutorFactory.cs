using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Data;
using StockAutoTrader.Infrastructure.Logging;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 交易执行器工厂
/// </summary>
public static class TradeExecutorFactory
{
    /// <summary>
    /// 创建交易执行器实例
    /// </summary>
    public static ITradeExecutor Create(
        ServiceSettings settings,
        IDbContextFactory<TradingDbContext> contextFactory,
        ILoggerService logger,
        INotificationService notificationService)
    {
        var orderManager = new EfOrderManager(contextFactory);
        var positionManager = new EfPositionManager(contextFactory);
        var accountManager = new EfAccountManager(contextFactory);

        return settings.TradeExecutor.ToLowerInvariant() switch
        {
            _ => new SimulatedTradeExecutor(
                orderManager,
                positionManager,
                accountManager,
                contextFactory,
                logger,
                notificationService,
                settings.CommissionRate,
                settings.Slippage)
        };
    }
}
