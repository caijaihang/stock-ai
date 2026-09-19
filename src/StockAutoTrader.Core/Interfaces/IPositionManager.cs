using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 持仓管理接口
/// </summary>
public interface IPositionManager
{
    /// <summary>
    /// 获取所有持仓
    /// </summary>
    Task<IReadOnlyList<Position>> GetAllPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据代码获取持仓
    /// </summary>
    Task<Position?> GetPositionAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 买入更新持仓
    /// </summary>
    Task<Position> BuyAsync(string stockCode, string stockName, int quantity, decimal price, DateTime tradeDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 卖出更新持仓
    /// </summary>
    Task<Position> SellAsync(string stockCode, int quantity, decimal price, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新最新价与市值
    /// </summary>
    Task UpdatePricesAsync(IReadOnlyDictionary<string, decimal> prices, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清仓
    /// </summary>
    Task LiquidateAllAsync(CancellationToken cancellationToken = default);
}
