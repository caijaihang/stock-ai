using System.Collections.Concurrent;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 顶层交易服务实现
/// </summary>
public class TradingService : ITradingService
{
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
    private readonly int _refreshIntervalMs;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private readonly ConcurrentDictionary<string, StockStatus> _runningStocks = new();
    private readonly SemaphoreSlim _tradeLock = new(1, 1);

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _monitorTask != null && !_monitorTask.IsCompleted;

    /// <summary>
    /// 刷新事件
    /// </summary>
    public event EventHandler? OnRefreshed;

    /// <summary>
    /// 构造函数
    /// </summary>
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

    /// <summary>
    /// 启动监控
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        await _marketDataProvider.ConnectAsync(cancellationToken);

        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        foreach (var config in configs.Where(c => c.Status == StockStatus.Running))
        {
            _runningStocks[config.StockCode] = StockStatus.Running;
        }

        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token), _cts.Token);

        _logger.Info("交易监控服务已启动", "TradingService");
        _notificationService.Notify("StockAutoTrader", "监控服务已启动");
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
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
        _logger.Info("交易监控服务已停止", "TradingService");
        _notificationService.Notify("StockAutoTrader", "监控服务已停止");
    }

    /// <summary>
    /// 启动指定股票监控
    /// </summary>
    public async Task StartStockAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await _stockRepository.UpdateStatusAsync(stockCode, StockStatus.Running, cancellationToken: cancellationToken);
        _runningStocks[stockCode] = StockStatus.Running;
        _logger.Info($"启动股票 {stockCode} 监控", "TradingService", stockCode);
    }

    /// <summary>
    /// 停止指定股票监控
    /// </summary>
    public async Task StopStockAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await _stockRepository.UpdateStatusAsync(stockCode, StockStatus.Stopped, cancellationToken: cancellationToken);
        _runningStocks.TryRemove(stockCode, out _);
        _logger.Info($"停止股票 {stockCode} 监控", "TradingService", stockCode);
    }

    /// <summary>
    /// 全部启动
    /// </summary>
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        foreach (var config in configs)
        {
            await StartStockAsync(config.StockCode, cancellationToken);
        }
        _logger.Info("全部启动完成", "TradingService");
    }

    /// <summary>
    /// 全部停止
    /// </summary>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        foreach (var config in configs)
        {
            await StopStockAsync(config.StockCode, cancellationToken);
        }
        _logger.Info("全部停止完成", "TradingService");
    }

    /// <summary>
    /// 一键清仓
    /// </summary>
    public async Task LiquidateAllAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        var positions = await _positionManager.GetAllPositionsAsync(cancellationToken);
        foreach (var position in positions.Where(p => p.AvailableQuantity > 0))
        {
            await _tradeExecutor.SellAsync(position.StockCode, position.StockName, position.AvailableQuantity, position.CurrentPrice, OrderType.SimulatedImmediate, cancellationToken);
        }
        _logger.Info("一键清仓完成", "TradingService");
        _notificationService.Notify("StockAutoTrader", "一键清仓完成");
    }

    /// <summary>
    /// 手动下单
    /// </summary>
    public Task<Order> PlaceManualOrderAsync(string stockCode, OrderSide side, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default)
    {
        return side == OrderSide.Buy
            ? _tradeExecutor.BuyAsync(stockCode, stockCode, quantity, price, orderType, cancellationToken)
            : _tradeExecutor.SellAsync(stockCode, stockCode, quantity, price, orderType, cancellationToken);
    }

    /// <summary>
    /// 获取账户
    /// </summary>
    public Task<Account> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        return _accountManager.GetAccountAsync(cancellationToken);
    }

    /// <summary>
    /// 获取持仓
    /// </summary>
    public Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        return _positionManager.GetAllPositionsAsync(cancellationToken);
    }

    /// <summary>
    /// 获取委托
    /// </summary>
    public Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        return _orderManager.GetAllOrdersAsync(cancellationToken);
    }

    /// <summary>
    /// 获取成交（持久化）
    /// </summary>
    public Task<IReadOnlyList<Trade>> GetTradesAsync(CancellationToken cancellationToken = default)
    {
        return _tradeQueryService.GetTradesAsync(cancellationToken);
    }

    /// <summary>
    /// 获取日志
    /// </summary>
    public Task<IReadOnlyList<TradingLog>> GetLogsAsync(string? level = null, string? category = null, string? stockCode = null, CancellationToken cancellationToken = default)
    {
        return _tradeQueryService.GetLogsAsync(level, category, stockCode, cancellationToken);
    }

    /// <summary>
    /// 更新账户总市值
    /// </summary>
    public async Task UpdateAccountMarketValueAsync(CancellationToken cancellationToken = default)
    {
        var positions = await _positionManager.GetAllPositionsAsync(cancellationToken);
        var totalMarketValue = positions.Sum(p => p.MarketValue);
        await _accountManager.UpdateMarketValueAsync(totalMarketValue, cancellationToken);
    }

    /// <summary>
    /// 监控循环
    /// </summary>
    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(cancellationToken);
                OnRefreshed?.Invoke(this, EventArgs.Empty);
                await Task.Delay(_refreshIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("监控循环异常", ex, "TradingService");
                await Task.Delay(_refreshIntervalMs, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 单次刷新
    /// </summary>
    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        var configs = await _stockRepository.GetAllConfigsAsync(cancellationToken);
        var runningCodes = configs
            .Where(c => c.Status == StockStatus.Running && _runningStocks.ContainsKey(c.StockCode))
            .Select(c => c.StockCode)
            .ToList();

        if (runningCodes.Count == 0)
        {
            return;
        }

        var snapshots = await _marketDataProvider.GetSnapshotsAsync(runningCodes, cancellationToken);
        if (snapshots.Count == 0)
        {
            return;
        }

        var priceMap = snapshots.ToDictionary(s => s.StockCode, s => s.CurrentPrice);
        await _positionManager.UpdatePricesAsync(priceMap, cancellationToken);

        foreach (var snapshot in snapshots)
        {
            await EvaluateAndTradeAsync(snapshot, cancellationToken);
        }

        await UpdateAccountMarketValueAsync(cancellationToken);
    }

    /// <summary>
    /// 行情数据接收事件
    /// </summary>
    private async void OnMarketDataReceived(object? sender, MarketDataSnapshot snapshot)
    {
        try
        {
            await EvaluateAndTradeAsync(snapshot);
        }
        catch (Exception ex)
        {
            _logger.Error("行情事件处理异常", ex, "TradingService", snapshot.StockCode);
        }
    }

    /// <summary>
    /// 评估信号并执行交易
    /// </summary>
    private async Task EvaluateAndTradeAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var config = await _stockRepository.GetConfigAsync(snapshot.StockCode, cancellationToken);
        if (config == null || config.Status != StockStatus.Running)
        {
            return;
        }

        var position = await _positionManager.GetPositionAsync(snapshot.StockCode, cancellationToken);
        var todayBuyCount = await _orderManager.GetTodayBuyCountAsync(snapshot.StockCode, cancellationToken);
        var todaySellCount = await _orderManager.GetTodaySellCountAsync(snapshot.StockCode, cancellationToken);

        var signal = _strategyEngine.Evaluate(snapshot, config, position, todayBuyCount, todaySellCount);
        _logger.Strategy(snapshot.StockCode, signal.Reason);

        if (signal.SignalType == TradeSignalType.None)
        {
            return;
        }

        await _tradeLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查，防止重复下单
            var latestBuyCount = await _orderManager.GetTodayBuyCountAsync(snapshot.StockCode, cancellationToken);
            var latestSellCount = await _orderManager.GetTodaySellCountAsync(snapshot.StockCode, cancellationToken);

            if (signal.SignalType == TradeSignalType.Buy && latestBuyCount >= config.MaxBuyTimesPerDay)
            {
                return;
            }
            if (signal.SignalType == TradeSignalType.Sell && latestSellCount >= config.MaxSellTimesPerDay)
            {
                return;
            }

            Order order;
            if (signal.SignalType == TradeSignalType.Buy)
            {
                order = await _tradeExecutor.BuyAsync(
                    signal.StockCode,
                    signal.StockName,
                    signal.SuggestedQuantity,
                    signal.CurrentPrice,
                    signal.OrderType,
                    cancellationToken);

                await _stockRepository.UpdateStatusAsync(
                    signal.StockCode,
                    config.Status,
                    lastBuyTriggerTime: DateTime.Now,
                    cancellationToken: cancellationToken);
            }
            else
            {
                order = await _tradeExecutor.SellAsync(
                    signal.StockCode,
                    signal.StockName,
                    signal.SuggestedQuantity,
                    signal.CurrentPrice,
                    signal.OrderType,
                    cancellationToken);

                await _stockRepository.UpdateStatusAsync(
                    signal.StockCode,
                    config.Status,
                    lastSellTriggerTime: DateTime.Now,
                    cancellationToken: cancellationToken);
            }

            _logger.Order(snapshot.StockCode, $"{(signal.SignalType == TradeSignalType.Buy ? "买入" : "卖出")}下单：{order.OrderId}，状态 {order.Status}");
        }
        finally
        {
            _tradeLock.Release();
        }
    }
}
