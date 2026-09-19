using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Data;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 真实交易执行器（文件桥接模式）
///
/// 工作原理：
///   1. 本执行器把待处理订单写入 pending/ 目录（JSON 文件）
///   2. 用户侧的通达信/同花顺自动交易脚本（Python / 公式 / 第三方桥）
///      监控 pending/ 目录，读取订单并在真实交易软件中下单
///   3. 成交后脚本把成交回报写入 filled/ 目录（JSON 文件）
///   4. 本执行器轮询 filled/ 目录，读取回报并更新数据库
///
/// 这是最安全的"真实交易"对接方式，不注入、不破解、不绕过登录，
/// 完全通过文件交换完成，可直接投入通达信/同花顺生产环境。
/// </summary>
public class RealTradeExecutor : ITradeExecutor, IDisposable
{
    private readonly IOrderManager _orderManager;
    private readonly IPositionManager _positionManager;
    private readonly IAccountManager _accountManager;
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;
    private readonly ILoggerService _logger;
    private readonly INotificationService _notificationService;
    private readonly string _pendingDirectory;
    private readonly string _filledDirectory;
    private readonly decimal _commissionRate;
    private readonly Timer _fillWatcher;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);

    /// <summary>
    /// 交易源名称
    /// </summary>
    public string Name => "RealTradeExecutor";

    /// <summary>
    /// 是否模拟交易（false = 真实交易）
    /// </summary>
    public bool IsSimulated => false;

    /// <summary>
    /// 待处理订单目录
    /// </summary>
    public string PendingDirectory => _pendingDirectory;

    /// <summary>
    /// 已成交回报目录
    /// </summary>
    public string FilledDirectory => _filledDirectory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RealTradeExecutor(
        IOrderManager orderManager,
        IPositionManager positionManager,
        IAccountManager accountManager,
        IDbContextFactory<TradingDbContext> contextFactory,
        ILoggerService logger,
        INotificationService notificationService,
        string pendingDirectory,
        string filledDirectory,
        decimal commissionRate = 0.0003m,
        int fillWatcherIntervalMs = 1000)
    {
        _orderManager = orderManager;
        _positionManager = positionManager;
        _accountManager = accountManager;
        _contextFactory = contextFactory;
        _logger = logger;
        _notificationService = notificationService;
        _pendingDirectory = pendingDirectory;
        _filledDirectory = filledDirectory;
        _commissionRate = commissionRate;

        Directory.CreateDirectory(_pendingDirectory);
        Directory.CreateDirectory(_filledDirectory);

        _fillWatcher = new Timer(
            _ => _ = ProcessFilledOrdersAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(fillWatcherIntervalMs));
    }

    /// <summary>
    /// 买入：写入待处理订单文件，等待真实交易脚本处理
    /// </summary>
    public async Task<Order> BuyAsync(
        string stockCode,
        string stockName,
        int quantity,
        decimal price,
        OrderType orderType,
        CancellationToken cancellationToken = default)
    {
        var order = new Order
        {
            StockCode = stockCode,
            StockName = stockName,
            Side = OrderSide.Buy,
            OrderType = orderType,
            Quantity = quantity,
            Price = price,
            Status = OrderStatus.Pending,
            StrategyTrigger = "Auto",
            Remark = "RealTrade"
        };

        await _orderManager.CreateOrderAsync(order, cancellationToken);
        await WritePendingOrderFileAsync(order, cancellationToken);

        _logger.Order(stockCode, $"买入订单已写入待处理目录：{order.OrderId}，{quantity} 股 @ {price}，等待交易脚本处理");
        _notificationService.Notify($"买入 {stockName}", $"{quantity} 股 @ {price}（待确认）", isBuy: true);

        return order;
    }

    /// <summary>
    /// 卖出：写入待处理订单文件，等待真实交易脚本处理
    /// </summary>
    public async Task<Order> SellAsync(
        string stockCode,
        string stockName,
        int quantity,
        decimal price,
        OrderType orderType,
        CancellationToken cancellationToken = default)
    {
        var position = await _positionManager.GetPositionAsync(stockCode, cancellationToken);
        if (position == null || position.AvailableQuantity < quantity)
        {
            var rejected = new Order
            {
                StockCode = stockCode,
                StockName = stockName,
                Side = OrderSide.Sell,
                OrderType = orderType,
                Quantity = quantity,
                Price = price,
                Status = OrderStatus.Rejected,
                Remark = "可用持仓不足",
                StrategyTrigger = "Auto"
            };
            await _orderManager.CreateOrderAsync(rejected, cancellationToken);
            _logger.Error($"卖出订单被拒绝：可用持仓不足", category: "Order", stockCode: stockCode);
            return rejected;
        }

        var order = new Order
        {
            StockCode = stockCode,
            StockName = stockName,
            Side = OrderSide.Sell,
            OrderType = orderType,
            Quantity = quantity,
            Price = price,
            Status = OrderStatus.Pending,
            StrategyTrigger = "Auto",
            Remark = "RealTrade"
        };

        await _orderManager.CreateOrderAsync(order, cancellationToken);
        await WritePendingOrderFileAsync(order, cancellationToken);

        _logger.Order(stockCode, $"卖出订单已写入待处理目录：{order.OrderId}，{quantity} 股 @ {price}，等待交易脚本处理");
        _notificationService.Notify($"卖出 {stockName}", $"{quantity} 股 @ {price}（待确认）", isBuy: false);

        return order;
    }

    /// <summary>
    /// 撤单：写入撤单文件，由交易脚本执行撤单
    /// </summary>
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderManager.GetOrderAsync(orderId, cancellationToken);
        if (order == null)
        {
            return false;
        }

        var cancelPayload = new
        {
            OrderId = orderId,
            Action = "cancel",
            Timestamp = DateTime.Now.ToString("o")
        };

        var filePath = Path.Combine(_pendingDirectory, $"{orderId}_cancel.json");
        await File.WriteAllTextAsync(
            filePath,
            JsonSerializer.Serialize(cancelPayload, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        _logger.Order(order.StockCode, $"撤单请求已写入待处理目录：{orderId}");
        return true;
    }

    /// <summary>
    /// 查询资金（从数据库）
    /// </summary>
    public Task<Account> QueryAccountAsync(CancellationToken cancellationToken = default)
    {
        return _accountManager.GetAccountAsync(cancellationToken);
    }

    /// <summary>
    /// 查询持仓（从数据库）
    /// </summary>
    public Task<IReadOnlyList<Position>> QueryPositionsAsync(CancellationToken cancellationToken = default)
    {
        return _positionManager.GetAllPositionsAsync(cancellationToken);
    }

    /// <summary>
    /// 查询委托（从数据库）
    /// </summary>
    public Task<IReadOnlyList<Order>> QueryOrdersAsync(CancellationToken cancellationToken = default)
    {
        return _orderManager.GetAllOrdersAsync(cancellationToken);
    }

    /// <summary>
    /// 查询成交（从数据库）
    /// </summary>
    public async Task<IReadOnlyList<Trade>> QueryTradesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Trades
            .AsNoTracking()
            .OrderByDescending(t => t.TradeTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 写入待处理订单 JSON 文件
    /// </summary>
    private async Task WritePendingOrderFileAsync(Order order, CancellationToken cancellationToken)
    {
        var payload = new
        {
            OrderId = order.OrderId,
            StockCode = order.StockCode,
            StockName = order.StockName,
            Side = order.Side.ToString(),
            Quantity = order.Quantity,
            Price = order.Price,
            OrderType = order.OrderType.ToString(),
            Timestamp = DateTime.Now.ToString("o")
        };

        var filePath = Path.Combine(_pendingDirectory, $"{order.OrderId}.json");
        await File.WriteAllTextAsync(
            filePath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    /// <summary>
    /// 轮询处理已成交回报文件
    /// </summary>
    private async Task ProcessFilledOrdersAsync()
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        await _processLock.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_filledDirectory, "*.json");
            foreach (var file in files)
            {
                await ProcessSingleFilledFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理成交回报异常", ex, "RealTradeExecutor");
        }
        finally
        {
            _processLock.Release();
        }
    }

    /// <summary>
    /// 处理单个成交回报文件
    /// </summary>
    private async Task ProcessSingleFilledFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var fill = JsonSerializer.Deserialize<FilledOrderPayload>(json);
            if (fill == null || string.IsNullOrEmpty(fill.OrderId))
            {
                File.Delete(filePath);
                return;
            }

            var order = await _orderManager.GetOrderAsync(fill.OrderId, CancellationToken.None);
            if (order != null && order.Status == OrderStatus.Pending)
            {
                // 更新订单状态
                order.FilledQuantity = fill.Quantity;
                order.FilledPrice = fill.Price;
                order.Commission = fill.Commission;
                order.Status = OrderStatus.Filled;
                order.FillTime = fill.FillTime;
                await _orderManager.UpdateOrderAsync(order, CancellationToken.None);

                // 更新持仓与资金
                if (fill.Side == OrderSide.Buy)
                {
                    await _positionManager.BuyAsync(
                        fill.StockCode,
                        fill.StockName,
                        fill.Quantity,
                        fill.Price,
                        fill.FillTime,
                        CancellationToken.None);
                    await _accountManager.DeductForBuyAsync(
                        fill.Price * fill.Quantity + fill.Commission,
                        CancellationToken.None);
                }
                else
                {
                    await _positionManager.SellAsync(
                        fill.StockCode,
                        fill.Quantity,
                        fill.Price,
                        CancellationToken.None);
                    await _accountManager.RefundForSellAsync(
                        fill.Price * fill.Quantity - fill.Commission,
                        CancellationToken.None);
                }

                // 持久化成交记录
                await PersistTradeAsync(fill, CancellationToken.None);

                _logger.Trade(
                    fill.StockCode,
                    $"成交回报：{fill.OrderId}，{fill.Side} {fill.Quantity} 股 @ {fill.Price}，佣金 {fill.Commission}");
                _notificationService.Notify(
                    $"{(fill.Side == OrderSide.Buy ? "买入" : "卖出")}成交",
                    $"{fill.StockName} {fill.Quantity} 股 @ {fill.Price}",
                    fill.Side == OrderSide.Buy);
            }

            // 删除已处理的文件
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.Error($"处理成交回报文件失败：{filePath}", ex, "RealTradeExecutor");
        }
    }

    /// <summary>
    /// 持久化成交记录到数据库
    /// </summary>
    private async Task PersistTradeAsync(FilledOrderPayload fill, CancellationToken cancellationToken)
    {
        await using var context = _contextFactory.CreateDbContext();
        var trade = new Trade
        {
            OrderId = fill.OrderId,
            StockCode = fill.StockCode,
            StockName = fill.StockName,
            Side = fill.Side,
            Quantity = fill.Quantity,
            Price = fill.Price,
            Amount = fill.Price * fill.Quantity,
            Commission = fill.Commission,
            TradeTime = fill.FillTime
        };
        context.Trades.Add(trade);
        await context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _fillWatcher.Dispose();
        _cts.Dispose();
        _processLock.Dispose();
    }
}

/// <summary>
/// 成交回报文件载荷（JSON）
/// </summary>
public class FilledOrderPayload
{
    /// <summary>
    /// 订单 ID（与 pending 文件中的 OrderId 对应）
    /// </summary>
    public string OrderId { get; set; } = "";

    /// <summary>
    /// 股票代码
    /// </summary>
    public string StockCode { get; set; } = "";

    /// <summary>
    /// 股票名称
    /// </summary>
    public string StockName { get; set; } = "";

    /// <summary>
    /// 买卖方向
    /// </summary>
    public OrderSide Side { get; set; }

    /// <summary>
    /// 成交数量
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// 成交价格
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// 佣金（元）
    /// </summary>
    public decimal Commission { get; set; }

    /// <summary>
    /// 成交时间
    /// </summary>
    public DateTime FillTime { get; set; }
}
