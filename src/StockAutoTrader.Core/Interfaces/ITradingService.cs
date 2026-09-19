using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 顶层交易服务接口
/// </summary>
public interface ITradingService
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 启动监控
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止监控
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动指定股票监控
    /// </summary>
    Task StartStockAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止指定股票监控
    /// </summary>
    Task StopStockAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全部启动
    /// </summary>
    Task StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 全部停止
    /// </summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 一键清仓
    /// </summary>
    Task LiquidateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动下单
    /// </summary>
    Task<Order> PlaceManualOrderAsync(string stockCode, OrderSide side, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户
    /// </summary>
    Task<Account> GetAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取持仓
    /// </summary>
    Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取委托
    /// </summary>
    Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取成交
    /// </summary>
    Task<IReadOnlyList<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 行情刷新事件
    /// </summary>
    event EventHandler? OnRefreshed;
}
