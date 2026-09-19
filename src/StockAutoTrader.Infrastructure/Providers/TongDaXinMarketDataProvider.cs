using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 通达信行情数据提供器（真实本地数据读取）
///
/// 支持两种数据源：
///   1. .day 二进制日数据文件：直接读取通达信安装目录 vipdoc/&lt;market&gt;/lday/&lt;code&gt;.day
///      提供收盘价、昨收、开高低、成交量（真实历史数据，EOD 级别）
///   2. CSV 实时导出文件：用户在通达信里导出实时行情 CSV，放到 data/market 目录，
///      程序用 FileSystemWatcher 监听并解析，提供盘中实时快照
///
/// 涨跌幅 = (最新价 - 昨收) / 昨收 * 100
/// </summary>
public class TongDaXinMarketDataProvider : IMarketDataProvider, IDisposable
{
    private readonly string _csvDirectory;
    private readonly string _tdxInstallDirectory;
    private readonly ConcurrentDictionary<string, MarketDataSnapshot> _snapshots = new();
    private readonly object _watcherLock = new();
    private FileSystemWatcher? _watcher;

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
    public TongDaXinMarketDataProvider(string csvDirectory, string tdxInstallDirectory = "")
    {
        _csvDirectory = csvDirectory;
        _tdxInstallDirectory = tdxInstallDirectory;
        Directory.CreateDirectory(_csvDirectory);
    }

    /// <summary>
    /// 连接行情源（启动文件监听）
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        lock (_watcherLock)
        {
            if (_watcher == null)
            {
                _watcher = new FileSystemWatcher(_csvDirectory, "*.csv")
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };
                _watcher.Changed += OnCsvChanged;
                _watcher.Created += OnCsvChanged;
                _watcher.EnableRaisingEvents = true;
            }
        }

        // 预载目录里已有的 CSV
        foreach (var file in Directory.GetFiles(_csvDirectory, "*.csv"))
        {
            TryLoadCsv(file);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 断开行情源
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        lock (_watcherLock)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取单只股票快照（优先 CSV 实时，回退 .day 收盘数据）
    /// </summary>
    public Task<MarketDataSnapshot?> GetSnapshotAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        if (_snapshots.TryGetValue(stockCode, out var cached) && cached.Timestamp > DateTime.Now.AddMinutes(-5))
        {
            return Task.FromResult(cached);
        }

        // 回退：读取 .day 文件提供 EOD 数据
        var daySnapshot = TryReadDayFile(stockCode);
        if (daySnapshot != null)
        {
            _snapshots[stockCode] = daySnapshot;
            return Task.FromResult(daySnapshot);
        }

        return Task.FromResult(_snapshots.TryGetValue(stockCode, out var s) ? s : null);
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
    /// CSV 文件变更回调
    /// </summary>
    private void OnCsvChanged(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(e.Name, Path.GetFileName(e.FullPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        TryLoadCsv(e.FullPath);
    }

    /// <summary>
    /// 尝试加载 CSV 文件到缓存
    /// </summary>
    private void TryLoadCsv(string filePath)
    {
        try
        {
            var snapshot = ReadFromCsv(filePath);
            if (snapshot != null)
            {
                _snapshots[snapshot.StockCode] = snapshot;
                OnMarketData?.Invoke(this, snapshot);
            }
        }
        catch
        {
            // 文件正在写入时读取可能失败，忽略
        }
    }

    /// <summary>
    /// 从 CSV 文件读取行情快照
    /// 支持格式：代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量,时间
    /// 也支持通达信导出格式：代码 名称 最新价 昨收 今开 最高 最低 成交量 成交额（制表符分隔）
    /// </summary>
    private static MarketDataSnapshot? ReadFromCsv(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var lines = File.ReadAllLines(filePath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        // 取最后一行数据（跳过表头）
        var lastLine = lines[^1];
        string[] parts;

        if (lastLine.Contains('\t'))
        {
            // 通达信制表符导出格式
            parts = lastLine.Split('\t');
            if (parts.Length < 9)
            {
                return null;
            }
            var code = parts[0].Trim();
            var name = parts[1].Trim();
            var price = ParseDecimal(parts[2]);
            var prevClose = ParseDecimal(parts[3]);
            var open = ParseDecimal(parts[4]);
            var high = ParseDecimal(parts[5]);
            var low = ParseDecimal(parts[6]);
            var volume = long.TryParse(parts[7].Trim(), out var v) ? v : 0;
            var changePct = prevClose > 0 ? (price - prevClose) / prevClose * 100m : 0m;

            return new MarketDataSnapshot
            {
                StockCode = code,
                StockName = name,
                CurrentPrice = price,
                ChangePercent = changePct,
                PreviousClose = prevClose,
                Open = open,
                High = high,
                Low = low,
                Volume = volume,
                Timestamp = DateTime.Now
            };
        }
        else
        {
            // 逗号分隔格式：代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量,时间
            parts = lastLine.Split(',');
            if (parts.Length < 10)
            {
                return null;
            }
            var code = parts[0].Trim();
            var name = parts[1].Trim();
            var price = ParseDecimal(parts[2]);
            var changePct = ParseDecimal(parts[3]);
            var prevClose = ParseDecimal(parts[4]);
            var open = ParseDecimal(parts[5]);
            var high = ParseDecimal(parts[6]);
            var low = ParseDecimal(parts[7]);
            var volume = long.TryParse(parts[8].Trim(), out var v2) ? v2 : 0;

            return new MarketDataSnapshot
            {
                StockCode = code,
                StockName = name,
                CurrentPrice = price,
                ChangePercent = changePct,
                PreviousClose = prevClose,
                Open = open,
                High = high,
                Low = low,
                Volume = volume,
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 读取 .day 日数据文件（通达信本地真实收盘数据）
    /// </summary>
    private MarketDataSnapshot? TryReadDayFile(string stockCode)
    {
        if (string.IsNullOrWhiteSpace(_tdxInstallDirectory))
        {
            return null;
        }

        var dayFilePath = TdxDayFileReader.ResolveDayFilePath(_tdxInstallDirectory, stockCode);
        var lastClose = TdxDayFileReader.ReadLastClose(dayFilePath);
        if (lastClose == null)
        {
            return null;
        }

        // 读取最后两条记录获取昨收
        var records = TdxDayFileReader.ReadLastNDays(dayFilePath, 2);
        var prevClose = records.Count >= 2 ? records[^2].Close : records[^1].Open;

        return new MarketDataSnapshot
        {
            StockCode = stockCode,
            StockName = stockCode,
            CurrentPrice = lastClose.Value,
            PreviousClose = prevClose,
            ChangePercent = prevClose > 0 ? (lastClose.Value - prevClose) / prevClose * 100m : 0m,
            Open = records.Count > 0 ? records[^1].Open : 0,
            High = records.Count > 0 ? records[^1].High : 0,
            Low = records.Count > 0 ? records[^1].Low : 0,
            Volume = records.Count > 0 ? (long)records[^1].Volume : 0,
            Timestamp = DateTime.Now
        };
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
    }

    public void Dispose()
    {
        lock (_watcherLock)
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }
}
