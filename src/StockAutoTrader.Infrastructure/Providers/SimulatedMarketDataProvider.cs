using System.Collections.Concurrent;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 模拟行情数据提供器（实时随机游走，可配置波动率与刷新频率）
/// </summary>
public class SimulatedMarketDataProvider : IMarketDataProvider
{
    private readonly ConcurrentDictionary<string, MarketDataSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<string, string> _names = new();
    private readonly Random _random = new();
    private readonly object _lock = new();
    private decimal _volatility;
    private int _intervalMs;
    private Timer? _timer;

    /// <summary>
    /// 行情源名称
    /// </summary>
    public string Name => "Simulated";

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
    public SimulatedMarketDataProvider(double volatility = 0.005, int intervalMs = 1000)
    {
        _volatility = (decimal)volatility;
        _intervalMs = intervalMs;
    }

    /// <summary>
    /// 连接行情源
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsConnected)
            {
                return Task.CompletedTask;
            }

            IsConnected = true;
            _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_intervalMs));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 断开行情源
    /// </summary>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IsConnected = false;
            _timer?.Dispose();
            _timer = null;
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
    public Task<IReadOnlyList<MarketDataSnapshot>> GetSnapshotsAsync(IEnumerable<string> stockCodes, CancellationToken cancellationToken = default)
    {
        var list = stockCodes
            .Select(code => _snapshots.TryGetValue(code, out var snapshot) ? snapshot : null)
            .Where(s => s != null)
            .Cast<MarketDataSnapshot>()
            .ToList();
        return Task.FromResult<IReadOnlyList<MarketDataSnapshot>>(list);
    }

    /// <summary>
    /// 初始化股票基准数据
    /// </summary>
    public void InitializeStock(string stockCode, string stockName, decimal basePrice)
    {
        if (_snapshots.ContainsKey(stockCode))
        {
            return;
        }

        var snapshot = new MarketDataSnapshot
        {
            StockCode = stockCode,
            StockName = stockName,
            CurrentPrice = basePrice,
            PreviousClose = basePrice,
            Open = basePrice,
            High = basePrice,
            Low = basePrice,
            Volume = 0,
            Timestamp = DateTime.Now
        };
        _snapshots[stockCode] = snapshot;
        _names[stockCode] = stockName;
    }

    /// <summary>
    /// 设置波动率
    /// </summary>
    public void SetVolatility(double volatility)
    {
        _volatility = (decimal)volatility;
    }

    /// <summary>
    /// 手动触发一次行情更新（用于测试或回放）
    /// </summary>
    public void TickOnce()
    {
        OnTimerTick(null);
    }

    /// <summary>
    /// 定时更新模拟行情
    /// </summary>
    private void OnTimerTick(object? state)
    {
        foreach (var code in _snapshots.Keys.ToList())
        {
            if (!_snapshots.TryGetValue(code, out var snapshot))
            {
                continue;
            }

            // 随机游走：当前价 * (1 + 随机波动)
            var change = (_random.NextDouble() * 2 - 1) * (double)_volatility;
            var newPrice = Math.Round(snapshot.CurrentPrice * (1 + (decimal)change), 2);
            newPrice = newPrice <= 0 ? snapshot.PreviousClose : newPrice;

            var newSnapshot = snapshot with
            {
                CurrentPrice = newPrice,
                High = Math.Max(snapshot.High, newPrice),
                Low = Math.Min(snapshot.Low, newPrice),
                ChangePercent = snapshot.PreviousClose > 0
                    ? (newPrice - snapshot.PreviousClose) / snapshot.PreviousClose * 100m
                    : 0,
                Volume = snapshot.Volume + _random.Next(50, 5000),
                Timestamp = DateTime.Now
            };

            _snapshots[code] = newSnapshot;
            OnMarketData?.Invoke(this, newSnapshot);
        }
    }
}
