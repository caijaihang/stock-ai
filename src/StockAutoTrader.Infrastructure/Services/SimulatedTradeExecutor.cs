using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Data;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 模拟交易执行器（内置撮合，按 A 股真实费率扣费）
/// 费用规则：
///   佣金 = max(成交额 * 佣金率, 5.0)，买卖双向
///   印花税 = 成交额 * 0.05%，仅卖出
///   过户费 = 成交额 * 0.001%，买卖双向（沪市）
/// </summary>
public class SimulatedTradeExecutor : ITradeExecutor
{
    private const decimal MinCommission = 5.0m;
    private const decimal StampTaxRate = 0.0005m;
    private const decimal TransferFeeRate = 0.00001m;

    private readonly IOrderManager _orderManager;
    private readonly IPositionManager _positionManager;
    private readonly IAccountManager _accountManager;
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;
    private readonly ILoggerService _logger;
    private readonly INotificationService _notificationService;
    private readonly decimal _commissionRate;
    private readonly decimal _slippage;

    public string Name => "SimulatedTradeExecutor";
    public bool IsSimulated => true;

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
    /// 买入：成交额 + 佣金 + 过户费 从可用资金扣除
    /// </summary>
    public async Task<Order> BuyAsync(string stockCode, string stockName, int quantity, decimal price, OrderType orderType, CancellationToken cancellationToken = default)
    {
        var filledPrice = ApplySlippage(price, OrderSide.Buy);
        var amount = filledPrice * quantity;
        var commission = Math.Max(amount * _commissionRate, MinCommission);
        var transferFee = amount * TransferFeeRate;
        var totalCost = amount + commission + transferFee;

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
            StampTax = 0m,
            TransferFee = transferFee,
            Status = OrderStatus.Filled,
            FillTime = DateTime.Now,
            StrategyTrigger = "Auto"
        };

        await _orderManager.CreateOrderAsync(order, cancellationToken);
        await _positionManager.BuyAsync(stockCode, stockName, quantity, filledPrice, DateTime.Now, cancellationToken);
        await PersistTradeAsync(order, cancellationToken);

        _logger.Trade(stockCode, $"买入成交 {quantity} 股 @ {filledPrice:C}，佣金 {commission:C}，过户费 {transferFee:C}");
        _notificationService.Notify($"买入 {stockName}", $"{quantity} 股 @ {filledPrice}", isBuy: true);
        return order;
    }

    /// <summary>
    /// 卖出：成交额 - 佣金 - 印花税 - 过户费 回退可用资金
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
        var commission = Math.Max(amount * _commissionRate, MinCommission);
        var stampTax = amount * StampTaxRate;
        var transferFee = amount * TransferFeeRate;
        var netAmount = amount - commission - stampTax - transferFee;

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
            StampTax = stampTax,
            TransferFee = transferFee,
            Status = OrderStatus.Filled,
            FillTime = DateTime.Now,
            StrategyTrigger = "Auto"
        };

        await _orderManager.CreateOrderAsync(order, cancellationToken);
        await _positionManager.SellAsync(stockCode, quantity, filledPrice, cancellationToken);
        await _accountManager.RefundForSellAsync(netAmount, cancellationToken);
        await PersistTradeAsync(order, cancellationToken);

        _logger.Trade(stockCode, $"卖出成交 {quantity} 股 @ {filledPrice:C}，佣金 {commission:C}，印花税 {stampTax:C}，过户费 {transferFee:C}");
        _notificationService.Notify($"卖出 {stockName}", $"{quantity} 股 @ {filledPrice}", isBuy: false);
        return order;
    }

    public Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return _orderManager.CancelOrderAsync(orderId, cancellationToken);
    }

    public Task<Account> QueryAccountAsync(CancellationToken cancellationToken = default)
    {
        return _accountManager.GetAccountAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Position>> QueryPositionsAsync(CancellationToken cancellationToken = default)
    {
        return _positionManager.GetAllPositionsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Order>> QueryOrdersAsync(CancellationToken cancellationToken = default)
    {
        return _orderManager.GetAllOrdersAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trade>> QueryTradesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Trades
            .AsNoTracking()
            .OrderByDescending(t => t.TradeTime)
            .ToListAsync(cancellationToken);
    }

    private decimal ApplySlippage(decimal price, OrderSide side)
    {
        return side == OrderSide.Buy
            ? price * (1 + _slippage)
            : price * (1 - _slippage);
    }

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
            StampTax = order.StampTax,
            TransferFee = order.TransferFee,
            TradeTime = order.FillTime ?? order.OrderTime
        };
        context.Trades.Add(trade);
        await context.SaveChangesAsync(cancellationToken);
    }
}
