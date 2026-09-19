using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Data;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 模拟交易执行器（内置撮合）
/// </summary>
public class SimulatedTradeExecutor : ITradeExecutor
{
    private readonly IOrderManager _orderManager;
    private readonly IPositionManager _positionManager;
    private readonly IAccountManager _accountManager;
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;
    private readonly ILoggerService _logger;
    private readonly INotificationService _notificationService;
    private readonly decimal _commissionRate;
    private readonly decimal _slippage;

    /// <summary>
    /// 交易源名称
    /// </summary>
    public string Name => "SimulatedTradeExecutor";

    /// <summary>
    /// 是否模拟交易
    /// </summary>
    public bool IsSimulated => true;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SimulatedTradeExecutor(
        IOrderManager orderManager,
        IPositionManager positionManager,
        IAccountManager accountManager,
        IDbContextFactory<TradingDbContext> contextFactory,
        ILoggerService logger,
        INotificationService notificationService,
        decimal commissionRate = 0.0003m,
        decimal slippage = 0.0m)
    {
        _orderManager = orderManager;
        _positionManager = positionManager;
        _accountManager = accountManager;
        _contextFactory = contextFactory;
        _logger = logger;
        _notificationService = notificationService;
        _commissionRate = commissionRate;
        _slippage = slippage;
    }

    /// <summary>
    /// 买入
    /// </summary>
    public async Task<Order> BuyAsync(string stockCode, string stockName, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default)
    {
        var filledPrice = ApplySlippage(price, OrderSide.Buy);
        var amount = filledPrice * quantity;
        var commission = amount * _commissionRate;
        var totalCost = amount + commission;

        var canDeduct = await _accountManager.DeductForBuyAsync(totalCost, cancellationToken);
        if (!canDeduct)
        {
            return await RejectOrderAsync(stockCode, stockName, OrderSide.Buy, quantity, price, "资金不足");
        }

        var order = new Order
        {
            StockCode = stockCode,
            StockName = stockName,
            Side = OrderSide.Buy,
            OrderType = orderType,
            Quantity = quantity,
            FilledQuantity = quantity,
            Price = price,
            FilledPrice = filledPrice,
            Commission = commission,
            Status = OrderStatus.Filled,
            FillTime = DateTime.Now,
            StrategyTrigger = "Auto"
        };

        await _orderManager.CreateOrderAsync(order, cancellationToken);
        await _positionManager.BuyAsync(stockCode, stockName, quantity, filledPrice, DateTime.Now, cancellationToken);
        await PersistTradeAsync(order, cancellationToken);

        _logger.Trade(stockCode, $"买入成交 {quantity} 股，成交价 {filledPrice:C}，佣金 {commission:C}");
        _notificationService.Notify($"买入 {stockName}", $"{quantity} 股 @ {filledPrice}", isBuy: true);
        return order;
    }

    /// <summary>
    /// 卖出
    /// </summary>
    public async Task<Order> SellAsync(string stockCode, string stockName, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default)
    {
        var position = await _positionManager.GetPositionAsync(stockCode, cancellationToken);
        if (position == null || position.AvailableQuantity < quantity)
        {
            return await RejectOrderAsync(stockCode, stockName, OrderSide.Sell, quantity, price, "可用持仓不足");
        }

        var filledPrice = ApplySlippage(price, OrderSide.Sell);
        var amount = filledPrice * quantity;
        var commission = amount * _commissionRate;
        var netAmount = amount - commission;

        var order = new Order
        {
            StockCode = stockCode,
            StockName = stockName,
            Side = OrderSide.Sell,
            OrderType = orderType,
            Quantity = quantity,
            FilledQuantity = quantity,
            Price = price,
            FilledPrice = filledPrice,
            Commission = commission,
            Status = OrderStatus.Filled,
            FillTime = DateTime.Now,
            StrategyTrigger = "Auto"
        };

        await _orderManager.CreateOrderAsync(order, cancellationToken);
        await _positionManager.SellAsync(stockCode, quantity, filledPrice, cancellationToken);
        await _accountManager.RefundForSellAsync(netAmount, cancellationToken);
        await PersistTradeAsync(order, cancellationToken);

        _logger.Trade(stockCode, $"卖出成交 {quantity} 股，成交价 {filledPrice:C}，佣金 {commission:C}");
        _notificationService.Notify($"卖出 {stockName}", $"{quantity} 股 @ {filledPrice}", isBuy: false);
        return order;
    }

    /// <summary>
    /// 撤单
    /// </summary>
    public Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return _orderManager.CancelOrderAsync(orderId, cancellationToken);
    }

    /// <summary>
    /// 查询资金
    /// </summary>
    public Task<Account> QueryAccountAsync(CancellationToken cancellationToken = default)
    {
        return _accountManager.GetAccountAsync(cancellationToken);
    }

    /// <summary>
    /// 查询持仓
    /// </summary>
    public Task<IReadOnlyList<Position>> QueryPositionsAsync(CancellationToken cancellationToken = default)
    {
        return _positionManager.GetAllPositionsAsync(cancellationToken);
    }

    /// <summary>
    /// 查询委托
    /// </summary>
    public Task<IReadOnlyList<Order>> QueryOrdersAsync(CancellationToken cancellationToken = default)
    {
        return _orderManager.GetAllOrdersAsync(cancellationToken);
    }

    /// <summary>
    /// 查询成交（持久化）
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
    /// 应用滑点
    /// </summary>
    private decimal ApplySlippage(decimal price, OrderSide side)
    {
        return side == OrderSide.Buy
            ? price * (1 + _slippage)
            : price * (1 - _slippage);
    }

    /// <summary>
    /// 生成拒绝订单
    /// </summary>
    private async Task<Order> RejectOrderAsync(string stockCode, string stockName, OrderSide side, int quantity, decimal price, string reason)
    {
        var order = new Order
        {
            StockCode = stockCode,
            StockName = stockName,
            Side = side,
            Quantity = quantity,
            Price = price,
            Status = OrderStatus.Rejected,
            Remark = reason,
            StrategyTrigger = "Auto"
        };
        await _orderManager.CreateOrderAsync(order);
        _logger.Error($"订单被拒绝：{reason}", category: "Order", stockCode: stockCode);
        return order;
    }

    /// <summary>
    /// 持久化成交记录
    /// </summary>
    private async Task PersistTradeAsync(Order order, CancellationToken cancellationToken)
    {
        await using var context = _contextFactory.CreateDbContext();
        var trade = new Trade
        {
            OrderId = order.OrderId,
            StockCode = order.StockCode,
            StockName = order.StockName,
            Side = order.Side,
            Quantity = order.FilledQuantity,
            Price = order.FilledPrice,
            Amount = order.FilledPrice * order.FilledQuantity,
            Commission = order.Commission,
            TradeTime = order.FillTime ?? order.OrderTime
        };
        context.Trades.Add(trade);
        await context.SaveChangesAsync(cancellationToken);
    }
}
