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
    /// 一键暂停（保持行情连接，暂停策略判断）
    /// </summary>
    Task PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复暂停的监控
    /// </summary>
    Task ResumeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否处于暂停状态
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// 修改刷新间隔（毫秒）
    /// </summary>
    void SetRefreshInterval(int intervalMs);

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
    /// 获取日志（支持按级别/分类/股票筛选）
    /// </summary>
    Task<IReadOnlyList<TradingLog>> GetLogsAsync(string? level = null, string? category = null, string? stockCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤单
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 行情刷新事件
    /// </summary>
    event EventHandler? OnRefreshed;
}
