using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 行情源接口
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// 行情源名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接行情源
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开行情源
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单只股票快照
    /// </summary>
    Task<MarketDataSnapshot?> GetSnapshotAsync(string stockCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取行情快照
    /// </summary>
    Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotsAsync(IEnumerable<string> stockCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅实时行情（如支持）
    /// </summary>
    event EventHandler<MarketDataSnapshot>? OnMarketData;
}
