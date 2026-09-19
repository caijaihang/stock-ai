using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 交易执行接口
/// </summary>
public interface ITradeExecutor
{
    /// <summary>
    /// 交易源名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否模拟交易
    /// </summary>
    bool IsSimulated { get; }

    /// <summary>
    /// 买入
    /// </summary>
    Task<Order> BuyAsync(string stockCode, string stockName, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 卖出
    /// </summary>
    Task<Order> SellAsync(string stockCode, string stockName, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤单
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询资金
    /// </summary>
    Task<Account> QueryAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询持仓
    /// </summary>
    Task<IReadOnlyList<Position>> QueryPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询委托
    /// </summary>
    Task<IReadOnlyList<Order>> QueryOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询成交（持久化）
    /// </summary>
    Task<IReadOnlyList<Trade>> QueryTradesAsync(CancellationToken cancellationToken = default);
}
