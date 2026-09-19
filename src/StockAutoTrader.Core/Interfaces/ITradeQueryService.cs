using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 交易数据查询接口（成交与日志持久化查询）
/// </summary>
public interface ITradeQueryService
{
    /// <summary>
    /// 查询全部成交
    /// </summary>
    Task<IReadOnlyList<Trade>> GetTradesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询指定股票成交
    /// </summary>
    Task<IReadOnlyList<Trade>> GetTradesByStockAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询日志（可按级别/分类/股票代码筛选）
    /// </summary>
    Task<IReadOnlyList<TradingLog>> GetLogsAsync(string? level = null, string? category = null, string? stockCode = null, CancellationToken cancellationToken = default);
}
