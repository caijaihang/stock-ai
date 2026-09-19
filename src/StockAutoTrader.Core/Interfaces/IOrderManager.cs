using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 订单管理接口
/// </summary>
public interface IOrderManager
{
    /// <summary>
    /// 创建订单
    /// </summary>
    Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新订单状态
    /// </summary>
    Task UpdateOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销订单
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询所有委托
    /// </summary>
    Task<IReadOnlyList<Order>> GetAllOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询今日委托
    /// </summary>
    Task<IReadOnlyList<Order>> GetTodayOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据股票代码查询今日委托
    /// </summary>
    Task<IReadOnlyList<Order>> GetTodayOrdersByStockAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取今日买入次数
    /// </summary>
    Task<int> GetTodayBuyCountAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取今日卖出次数
    /// </summary>
    Task<int> GetTodaySellCountAsync(string stockCode, CancellationToken cancellationToken = default);
}
