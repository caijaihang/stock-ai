using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 股票池与配置持久化接口
/// </summary>
public interface IStockRepository
{
    /// <summary>
    /// 获取所有股票配置
    /// </summary>
    Task<IReadOnlyList<StockConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据代码获取配置
    /// </summary>
    Task<StockConfig?> GetConfigAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加或更新配置
    /// </summary>
    Task<StockConfig> SaveConfigAsync(StockConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除配置
    /// </summary>
    Task<bool> DeleteConfigAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新状态与触发时间
    /// </summary>
    Task UpdateStatusAsync(string stockCode, StockStatus status, DateTime? lastBuyTriggerTime = null, DateTime? lastSellTriggerTime = null, CancellationToken cancellationToken = default);
}
