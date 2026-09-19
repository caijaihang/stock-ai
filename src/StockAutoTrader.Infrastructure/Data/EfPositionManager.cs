using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Infrastructure.Data;

/// <summary>
/// 持仓管理 EF 实现
/// </summary>
public class EfPositionManager : IPositionManager
{
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;

    public EfPositionManager(IDbContextFactory<TradingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 获取所有持仓
    /// </summary>
    public async Task<IReadOnlyList<Position>> GetAllPositionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Positions
            .AsNoTracking()
            .OrderBy(p => p.StockCode)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据代码获取持仓
    /// </summary>
    public async Task<Position?> GetPositionAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Positions
            .FirstOrDefaultAsync(p => p.StockCode == stockCode, cancellationToken);
    }

    /// <summary>
    /// 买入更新持仓
    /// </summary>
    public async Task<Position> BuyAsync(string stockCode, string stockName, int quantity, decimal price, DateTime tradeDate, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var position = await context.Positions
            .FirstOrDefaultAsync(p => p.StockCode == stockCode, cancellationToken);

        if (position == null)
        {
            position = new Position
            {
                StockCode = stockCode,
                StockName = stockName,
                TotalQuantity = quantity,
                AvailableQuantity = quantity,
                AverageCost = price,
                CurrentPrice = price,
                LastBuyDate = tradeDate,
                UpdatedAt = DateTime.Now
            };
            context.Positions.Add(position);
        }
        else
        {
            var totalCost = position.AverageCost * position.TotalQuantity + price * quantity;
            position.TotalQuantity += quantity;
            position.AvailableQuantity += quantity;
            position.AverageCost = position.TotalQuantity > 0 ? totalCost / position.TotalQuantity : 0;
            position.LastBuyDate = tradeDate;
            position.UpdatedAt = DateTime.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
        return position;
    }

    /// <summary>
    /// 卖出更新持仓
    /// </summary>
    public async Task<Position> SellAsync(string stockCode, int quantity, decimal price, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var position = await context.Positions
            .FirstOrDefaultAsync(p => p.StockCode == stockCode, cancellationToken);

        if (position == null)
        {
            throw new InvalidOperationException($"未找到股票 {stockCode} 的持仓");
        }

        if (position.AvailableQuantity < quantity)
        {
            throw new InvalidOperationException($"股票 {stockCode} 可用持仓不足");
        }

        position.TotalQuantity -= quantity;
        position.AvailableQuantity -= quantity;
        position.UpdatedAt = DateTime.Now;

        if (position.TotalQuantity == 0)
        {
            position.AverageCost = 0;
        }

        await context.SaveChangesAsync(cancellationToken);
        return position;
    }

    /// <summary>
    /// 更新最新价与市值
    /// </summary>
    public async Task UpdatePricesAsync(IReadOnlyDictionary<string, decimal> prices, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        foreach (var (stockCode, price) in prices)
        {
            var position = await context.Positions
                .FirstOrDefaultAsync(p => p.StockCode == stockCode, cancellationToken);

            if (position == null)
            {
                continue;
            }

            position.CurrentPrice = price;
            position.MarketValue = price * position.TotalQuantity;
            position.UnrealizedPnl = position.TotalQuantity > 0
                ? (price - position.AverageCost) * position.TotalQuantity
                : 0;
            position.UnrealizedPnlPercent = position.AverageCost > 0
                ? (price - position.AverageCost) / position.AverageCost * 100m
                : 0;
            position.UpdatedAt = DateTime.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 清仓
    /// </summary>
    public async Task LiquidateAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var positions = await context.Positions.ToListAsync(cancellationToken);
        context.Positions.RemoveRange(positions);
        await context.SaveChangesAsync(cancellationToken);
    }
}
