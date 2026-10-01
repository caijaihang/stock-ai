using System.Collections.Concurrent;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 顶层交易服务实现（并发监控 + 涨跌停 + 暂停 + 重试）
/// </summary>
public class TradingService : ITradingService
{
    // A 股涨跌停幅度
    private const decimal NormalLimitPercent = 10m;
    private const decimal StLimitPercent = 5m;
    private const decimal ChinextLimitPercent = 20m;

    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ITradeExecutor _tradeExecutor;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IStockRepository _stockRepository;
    private readonly IOrderManager _orderManager;
    private readonly IPositionManager _positionManager;
    private readonly IAccountManager _accountManager;
    private readonly ITradeQueryService _tradeQueryService;
    private readonly ILoggerService _logger;
    private readonly INotificationService _notificationService;

    private int _refreshIntervalMs;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private readonly ConcurrentDictionary<string, StockStatus> _runningStocks = new();
    private readonly SemaphoreSlim _tradeLock = new(1, 1);
    private volatile bool _isPaused;
    private readonly ConcurrentDictionary<string, int> _failCount = new();
    private const int MaxFailCount = 5;

    public bool IsRunning => _monitorTask != null && !_monitorTask.IsCompleted;
    public bool IsPaused => _isPaused;
    public event EventHandler? OnRefreshed;

    public TradingService(
        IMarketDataProvider marketDataProvider,
        ITradeExecutor tradeExecutor,
        IStrategyEngine strategyEngine,
        IStockRepository stockRepository,
        IOrderManager orderManager,
        IPositionManager positionManager,
        IAccountManager accountManager,
        ITradeQueryService tradeQueryService,
        ILoggerService logger,
        INotificationService notificationService,
        int refreshIntervalMs = 3000)
    {
        _marketDataProvider = marketDataProvider;
        _tradeExecutor = tradeExecutor;
        _strategyEngine = strategyEngine;
        _stockRepository = stockRepository;
        _orderManager = orderManager;
        _positionManager = positionManager;
        _accountManager = accountManager;
        _tradeQueryService = tradeQueryService;
        _logger = logger;
        _notificationService = notificationService;
        _refreshIntervalMs = refreshIntervalMs;
        _marketDataProvider.OnMarketData += OnMarketDataReceived;
    }

    public void SetRefreshInterval(int intervalMs)
    {
        if (intervalMs < 500) intervalMs = 500;
        _refreshIntervalMs = intervalMs;
        _logger.Info($"刷新间隔已设置为 {intervalMs}ms", "TradingService");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;
        await _marketDataProvider.ConnectAsync(cancellationToken);

        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        foreach (var config in configs.Where(c => c.Status == StockStatus.Running))
        {
            _runningStocks[config.StockCode] = StockStatus.Running;
        }

        _isPaused = false;
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token), _cts.Token);
        _logger.Info("交易监控服务已启动", "TradingService");
        _notificationService.Notify("StockAutoTrader", "监控服务已启动");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.Warning("监控任务停止超时", "TradingService");
            }
            _monitorTask = null;
        }
        await _marketDataProvider.DisconnectAsync(cancellationToken);
        _runningStocks.Clear();
        _isPaused = false;
        _logger.Info("交易监控服务已停止", "TradingService");
        _notificationService.Notify("StockAutoTrader", "监控服务已停止");
    }

    public Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = true;
        _logger.Info("所有股票已暂停（保持行情连接）", "TradingService");
        return Task.CompletedTask;
    }

    public Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = false;
        _logger.Info("已恢复所有股票监控", "TradingService");
        return Task.CompletedTask;
    }

    public async Task StartStockAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await _stockRepository.UpdateStatusAsync(stockCode, StockStatus.Running, cancellationToken: cancellationToken);
        _runningStocks[stockCode] = StockStatus.Running;
        _logger.Info($"启动股票 {stockCode} 监控", "TradingService", stockCode);
    }

    public async Task StopStockAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await _stockRepository.UpdateStatusAsync(stockCode, StockStatus.Stopped, cancellationToken: cancellationToken);
        _runningStocks.TryRemove(stockCode, out _);
        _logger.Info($"停止股票 {stockCode} 监控", "TradingService", stockCode);
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        foreach (var config in configs)
        {
            await StartStockAsync(config.StockCode, cancellationToken);
        }
        _logger.Info("全部启动完成", "TradingService");
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        foreach (var config in configs)
        {
            await StopStockAsync(config.StockCode, cancellationToken);
        }
        _logger.Info("全部停止完成", "TradingService");
    }

    public async Task LiquidateAllAsync(CancellationToken cancellationToken = default)
    {
        await PauseAllAsync(cancellationToken);
        var positions = await _positionManager.GetAllPositionsAsync(cancellationToken);
        foreach (var position in positions.Where(p => p.AvailableQuantity > 0))
        {
            await _tradeExecutor.SellAsync(position.StockCode, position.StockName, position.AvailableQuantity, position.CurrentPrice, OrderType.SimulatedImmediate, cancellationToken);
        }
        _logger.Info("一键清仓完成", "TradingService");
        _notificationService.Notify("StockAutoTrader", "一键清仓完成");
    }

    public Task<Order> PlaceManualOrderAsync(string stockCode, OrderSide side, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default)
    {
        return side == OrderSide.Buy
            ? _tradeExecutor.BuyAsync(stockCode, stockCode, quantity, price, orderType, cancellationToken)
            : _tradeExecutor.SellAsync(stockCode, stockCode, quantity, price, orderType, cancellationToken);
    }

    public Task<Account> GetAccountAsync(CancellationToken cancellationToken = default) => _accountManager.GetAccountAsync(cancellationToken);
    public Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken = default) => _positionManager.GetAllPositionsAsync(cancellationToken);
    public Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken = default) => _orderManager.GetAllOrdersAsync(cancellationToken);
    public Task<IReadOnlyList<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) => _tradeQueryService.GetTradesAsync(cancellationToken);

    public Task<IReadOnlyList<TradingLog>> GetLogsAsync(string? level = null, string? category = null, string? stockCode = null, CancellationToken cancellationToken = default)
        => _tradeQueryService.GetLogsAsync(level, category, stockCode, cancellationToken);

    public async Task UpdateAccountMarketValueAsync(CancellationToken cancellationToken = default)
    {
        var positions = await _positionManager.GetAllPositionsAsync(cancellationToken);
        await _accountManager.UpdateMarketValueAsync(positions.Sum(p => p.MarketValue), cancellationToken);
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_isPaused)
                {
                    await RefreshOnceAsync(cancellationToken);
                }
                OnRefreshed?.Invoke(this, EventArgs.Empty);
                await Task.Delay(_refreshIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.Error("监控循环异常", ex, "TradingService");
                await Task.Delay(_refreshIntervalMs, cancellationToken);
            }
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        var runningCodes = configs
            .Where(c => c.Status == StockStatus.Running && _runningStocks.ContainsKey(c.StockCode))
            .Select(c => c.StockCode)
            .ToList();

        if (runningCodes.Count == 0) return;

        var snapshots = await _marketDataProvider.GetSnapshotsAsync(runningCodes, cancellationToken);
        if (snapshots.Count == 0) return;

        var priceMap = snapshots.ToDictionary(s => s.StockCode, s => s.CurrentPrice);
        await _positionManager.UpdatePricesAsync(priceMap, cancellationToken);

        // 并发评估所有股票（50+ 只也能快速处理）
        await Parallel.ForEachAsync(snapshots, new ParallelOptions
        {
            MaxDegreeOfParallelism = 8,
            CancellationToken = cancellationToken
        }, async (snapshot, ct) => await EvaluateAndTradeAsync(snapshot, ct));

        await UpdateAccountMarketValueAsync(cancellationToken);
    }

    private async void OnMarketDataReceived(object? sender, MarketDataSnapshot snapshot)
    {
        if (_isPaused) return;
        try { await EvaluateAndTradeAsync(snapshot); }
        catch (Exception ex) { _logger.Error("行情事件处理异常", ex, "TradingService", snapshot.StockCode); }
    }

    private async Task EvaluateAndTradeAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        // 涨跌停价格校验（A 股规则）
        if (!IsWithinPriceLimit(snapshot))
        {
            _failCount.AddOrUpdate(snapshot.StockCode, 1, (_, c) => c + 1);
            return;
        }

        var config = await _stockRepository.GetConfigAsync(snapshot.StockCode, cancellationToken);
        if (config == null || config.Status != StockStatus.Running) return;

        var position = await _positionManager.GetPositionAsync(snapshot.StockCode, cancellationToken);
        var todayBuyCount = await _orderManager.GetTodayBuyCountAsync(snapshot.StockCode, cancellationToken);
        var todaySellCount = await _orderManager.GetTodaySellCountAsync(snapshot.StockCode, cancellationToken);

        var signal = _strategyEngine.Evaluate(snapshot, config, position, todayBuyCount, todaySellCount);
        _logger.Strategy(snapshot.StockCode, signal.Reason);

        if (signal.SignalType == TradeSignalType.None)
        {
            _failCount.TryRemove(snapshot.StockCode, out _);
            return;
        }

        await _tradeLock.WaitAsync(cancellationToken);
        try
        {
            var latestBuyCount = await _orderManager.GetTodayBuyCountAsync(snapshot.StockCode, cancellationToken);
            var latestSellCount = await _orderManager.GetTodaySellCountAsync(snapshot.StockCode, cancellationToken);
            if (signal.SignalType == TradeSignalType.Buy && latestBuyCount >= config.MaxBuyTimesPerDay) return;
            if (signal.SignalType == TradeSignalType.Sell && latestSellCount >= config.MaxSellTimesPerDay) return;

            Order order;
            if (signal.SignalType == TradeSignalType.Buy)
            {
                order = await _tradeExecutor.BuyAsync(signal.StockCode, signal.StockName, signal.SuggestedQuantity, signal.CurrentPrice, signal.OrderType, cancellationToken);
                await _stockRepository.UpdateStatusAsync(signal.StockCode, config.Status, lastBuyTriggerTime: DateTime.Now, cancellationToken: cancellationToken);
            }
            else
            {
                order = await _tradeExecutor.SellAsync(signal.StockCode, signal.StockName, signal.SuggestedQuantity, signal.CurrentPrice, signal.OrderType, cancellationToken);
                await _stockRepository.UpdateStatusAsync(signal.StockCode, config.Status, lastSellTriggerTime: DateTime.Now, cancellationToken: cancellationToken);
            }
            _logger.Order(snapshot.StockCode, $"{(signal.SignalType == TradeSignalType.Buy ? "买入" : "卖出")}下单：{order.OrderId}，状态 {order.Status}");
        }
        finally { _tradeLock.Release(); }
    }

    /// <summary>
    /// 校验是否在涨跌停范围内（A 股：普通股 ±10%，ST ±5%，创业板/科创板 ±20%）
    /// </summary>
    private static bool IsWithinPriceLimit(MarketDataSnapshot snapshot)
    {
        if (snapshot.PreviousClose <= 0) return true;
        var code = snapshot.StockCode;
        decimal limit;
        if (code.StartsWith("300") || code.StartsWith("688") || code.StartsWith("301"))
            limit = ChinextLimitPercent;
        else if (snapshot.StockName.Contains("ST") || snapshot.StockName.Contains("*ST"))
            limit = StLimitPercent;
        else
            limit = NormalLimitPercent;

        var upper = snapshot.PreviousClose * (1 + limit / 100m);
        var lower = snapshot.PreviousClose * (1 - limit / 100m);
        // 保留 2 位小数（A 股价格最小单位 0.01）
        upper = Math.Round(upper, 2);
        lower = Math.Round(lower, 2);
        return snapshot.CurrentPrice >= lower && snapshot.CurrentPrice <= upper;
    }
}
