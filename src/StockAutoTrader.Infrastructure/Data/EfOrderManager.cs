using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Infrastructure.Data;

/// <summary>
/// 订单管理 EF 实现
/// </summary>
public class EfOrderManager : IOrderManager
{
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;

    public EfOrderManager(IDbContextFactory<TradingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    public async Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        order.OrderTime = DateTime.Now;
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        return order;
    }

    /// <summary>
    /// 更新订单状态
    /// </summary>
    public async Task UpdateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.Orders.FindAsync(new object[] { order.Id }, cancellationToken);
        if (existing != null)
        {
            context.Entry(existing).CurrentValues.SetValues(order);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 撤销订单
    /// </summary>
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var order = await context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
        if (order == null || order.Status == OrderStatus.Filled || order.Status == OrderStatus.Cancelled)
        {
            return false;
        }

        order.Status = OrderStatus.Cancelled;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 查询所有委托
    /// </summary>
    public async Task<IReadOnlyList<Order>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.OrderTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询今日委托
    /// </summary>
    public async Task<IReadOnlyList<Order>> GetTodayOrdersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var today = DateTime.Now.Date;
        return await context.Orders
            .AsNoTracking()
            .Where(o => o.OrderTime >= today)
            .OrderByDescending(o => o.OrderTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据股票代码查询今日委托
    /// </summary>
    public async Task<IReadOnlyList<Order>> GetTodayOrdersByStockAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var today = DateTime.Now.Date;
        return await context.Orders
            .AsNoTracking()
            .Where(o => o.StockCode == stockCode && o.OrderTime >= today)
            .OrderByDescending(o => o.OrderTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取今日买入次数
    /// </summary>
    public async Task<int> GetTodayBuyCountAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var today = DateTime.Now.Date;
        return await context.Orders
            .AsNoTracking()
            .CountAsync(o => o.StockCode == stockCode &&
                             o.Side == OrderSide.Buy &&
                             o.OrderTime >= today &&
                             (o.Status == OrderStatus.Filled || o.Status == OrderStatus.Submitted || o.Status == OrderStatus.PartiallyFilled),
                cancellationToken);
    }

    /// <summary>
    /// 获取今日卖出次数
    /// </summary>
    public async Task<int> GetTodaySellCountAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var today = DateTime.Now.Date;
        return await context.Orders
            .AsNoTracking()
            .CountAsync(o => o.StockCode == stockCode &&
                             o.Side == OrderSide.Sell &&
                             o.OrderTime >= today &&
                             (o.Status == OrderStatus.Filled || o.Status == OrderStatus.Submitted || o.Status == OrderStatus.PartiallyFilled),
                cancellationToken);
    }
}
