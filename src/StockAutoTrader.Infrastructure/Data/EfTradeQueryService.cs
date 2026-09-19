using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Infrastructure.Data;

/// <summary>
/// 成交与日志持久化查询服务
/// </summary>
public class EfTradeQueryService : ITradeQueryService
{
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;

    public EfTradeQueryService(IDbContextFactory<TradingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 查询全部成交
    /// </summary>
    public async Task<IReadOnlyList<Trade>> GetTradesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Trades
            .AsNoTracking()
            .OrderByDescending(t => t.TradeTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询指定股票成交
    /// </summary>
    public async Task<IReadOnlyList<Trade>> GetTradesByStockAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Trades
            .AsNoTracking()
            .Where(t => t.StockCode == stockCode)
            .OrderByDescending(t => t.TradeTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询日志（可按级别/分类/股票代码筛选）
    /// </summary>
    public async Task<IReadOnlyList<TradingLog>> GetLogsAsync(string? level = null, string? category = null, string? stockCode = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.TradingLogs.AsNoTracking().AsQueryable<TradingLog>();

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(l => l.Level == level);
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(l => l.Category == category);
        }
        if (!string.IsNullOrWhiteSpace(stockCode))
        {
            query = query.Where(l => l.StockCode == stockCode);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(2000)
            .ToListAsync(cancellationToken);
    }
}
