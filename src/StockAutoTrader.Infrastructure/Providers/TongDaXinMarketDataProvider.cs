using System.Globalization;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 通达信行情数据提供器（预留适配层）
/// </summary>
public class TongDaXinMarketDataProvider : IMarketDataProvider
{
    private readonly string _exportDirectory;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 行情源名称
    /// </summary>
    public string Name => "TongDaXin";

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 实时行情事件
    /// </summary>
    public event EventHandler<MarketDataSnapshot>? OnMarketData;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TongDaXinMarketDataProvider(string exportDirectory)
    {
        _exportDirectory = exportDirectory;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 连接行情源
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 断开行情源
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取单只股票快照（优先读取本地 CSV/TXT 导出文件）
    /// </summary>
    public async Task<MarketDataSnapshot?> GetSnapshotAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_exportDirectory, $"{stockCode}.csv");
        if (File.Exists(filePath))
        {
            return await ReadFromCsvAsync(filePath, stockCode, cancellationToken);
        }

        // 预留：可通过通达信公开接口或本地 DDE 获取
        return await Task.FromResult<MarketDataSnapshot?>(null);
    }

    /// <summary>
    /// 批量获取行情快照
    /// </summary>
    public async Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotsAsync(IEnumerable<string> stockCodes, CancellationToken cancellationToken = default)
    {
        var results = new List<MarketDataSnapshot>();
        foreach (var code in stockCodes)
        {
            var snapshot = await GetSnapshotAsync(code, cancellationToken);
            if (snapshot != null)
            {
                results.Add(snapshot);
            }
        }
        return results;
    }

    /// <summary>
    /// 从 CSV 文件读取行情快照
    /// </summary>
    private static async Task<MarketDataSnapshot?> ReadFromCsvAsync(string filePath, string stockCode, CancellationToken cancellationToken)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            if (lines.Length < 2)
            {
                return null;
            }

            // 假设 CSV 格式：代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量,时间
            var parts = lines[^1].Split(',');
            if (parts.Length < 10)
            {
                return null;
            }

            return new MarketDataSnapshot
            {
                StockCode = parts[0].Trim(),
                StockName = parts[1].Trim(),
                CurrentPrice = decimal.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0,
                ChangePercent = decimal.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var c) ? c : 0,
                PreviousClose = decimal.TryParse(parts[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var pc) ? pc : 0,
                Open = decimal.TryParse(parts[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var o) ? o : 0,
                High = decimal.TryParse(parts[6].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var h) ? h : 0,
                Low = decimal.TryParse(parts[7].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var l) ? l : 0,
                Volume = long.TryParse(parts[8].Trim(), out var v) ? v : 0,
                Timestamp = DateTime.TryParse(parts[9].Trim(), out var t) ? t : DateTime.Now
            };
        }
        catch
        {
            return null;
        }
    }
}
