using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 同花顺行情数据提供器（真实本地数据读取）
///
/// 工作原理：
///   用户在同花顺里导出实时行情 CSV，放到配置的目录（默认 data/market），
///   程序用 FileSystemWatcher 监听 *.csv / *.txt 文件，解析后推送到事件。
///
/// 支持的 CSV 格式（逗号分隔）：
///   代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量[,时间]
/// 支持制表符分隔的同花顺导出格式。
/// </summary>
public class TongHuaShunMarketDataProvider : IMarketDataProvider, IDisposable
{
    private readonly string _csvDirectory;
    private readonly ConcurrentDictionary<string, MarketDataSnapshot> _snapshots = new();
    private readonly FileSystemWatcher? _csvWatcher;
    private readonly FileSystemWatcher? _txtWatcher;

    /// <summary>
    /// 行情源名称
    /// </summary>
    public string Name => "TongHuaShun";

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
    public TongHuaShunMarketDataProvider(string csvDirectory)
    {
        _csvDirectory = csvDirectory;
        Directory.CreateDirectory(_csvDirectory);
    }

    /// <summary>
    /// 连接行情源（启动文件监听）
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;

        _csvWatcher = CreateWatcher("*.csv");
        _txtWatcher = CreateWatcher("*.txt");

        // 预载目录里已有的文件
        foreach (var file in Directory.GetFiles(_csvDirectory, "*.csv"))
        {
            TryLoadFile(file);
        }
        foreach (var file in Directory.GetFiles(_csvDirectory, "*.txt"))
        {
            TryLoadFile(file);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 断开行情源
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        if (_csvWatcher != null)
        {
            _csvWatcher.EnableRaisingEvents = false;
            _csvWatcher.Dispose();
        }
        if (_txtWatcher != null)
        {
            _txtWatcher.EnableRaisingEvents = false;
            _txtWatcher.Dispose();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取单只股票快照
    /// </summary>
    public Task<MarketDataSnapshot?> GetSnapshotAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        _snapshots.TryGetValue(stockCode, out var snapshot);
        return Task.FromResult(snapshot);
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
    /// 创建文件监听器
    /// </summary>
    private FileSystemWatcher CreateWatcher(string pattern)
    {
        var watcher = new FileSystemWatcher(_csvDirectory, pattern)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        watcher.Changed += (_, e) => TryLoadFile(e.FullPath);
        watcher.Created += (_, e) => TryLoadFile(e.FullPath);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    /// <summary>
    /// 尝试加载行情文件到缓存
    /// </summary>
    private void TryLoadFile(string filePath)
    {
        try
        {
            var snapshot = ReadFromFile(filePath);
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
    /// 从文件读取行情快照
    /// 同花顺支持逗号分隔（首列表头）和制表符分隔两种导出格式
    /// </summary>
    private static MarketDataSnapshot? ReadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var lines = File.ReadAllLines(filePath, Encoding.UTF8)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        // 同花顺 CSV 可能带表头，从倒数第二行开始找有效数据
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var snapshot = ParseLine(lines[i]);
            if (snapshot != null)
            {
                return snapshot;
            }
        }
        return null;
    }

    /// <summary>
    /// 解析单行数据
    /// </summary>
    private static MarketDataSnapshot? ParseLine(string line)
    {
        if (line.Contains('\t'))
        {
            // 制表符分隔：代码 名称 最新价 昨收 今开 最高 最低 成交量
            var parts = line.Split('\t');
            if (parts.Length < 8)
            {
                return null;
            }
            var code = parts[0].Trim();
            if (!IsValidStockCode(code))
            {
                return null;
            }
            var name = parts.Length > 1 ? parts[1].Trim() : code;
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
            // 逗号分隔：代码,名称,最新价,涨跌幅,昨收,开盘,最高,最低,成交量[,时间]
            var parts = line.Split(',');
            if (parts.Length < 9)
            {
                return null;
            }
            var code = parts[0].Trim();
            if (!IsValidStockCode(code))
            {
                return null;
            }
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
    /// 判断是否为有效股票代码（6 位数字）
    /// </summary>
    private static bool IsValidStockCode(string code)
    {
        return code.Length == 6 && code.All(char.IsDigit);
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
    }

    public void Dispose()
    {
        _csvWatcher?.Dispose();
        _txtWatcher?.Dispose();
    }
}
