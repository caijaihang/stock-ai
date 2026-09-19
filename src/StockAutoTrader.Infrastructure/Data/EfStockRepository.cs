using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Infrastructure.Data;

/// <summary>
/// 股票配置 EF 持久化实现
/// </summary>
public class EfStockRepository : IStockRepository
{
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;

    public EfStockRepository(IDbContextFactory<TradingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 获取所有股票配置
    /// </summary>
    public async Task<IReadOnlyList<StockConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.StockConfigs
            .AsNoTracking()
            .OrderBy(c => c.StockCode)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根据代码获取配置
    /// </summary>
    public async Task<StockConfig?> GetConfigAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.StockConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.StockCode == stockCode, cancellationToken);
    }

    /// <summary>
    /// 添加或更新配置
    /// </summary>
    public async Task<StockConfig> SaveConfigAsync(StockConfig config, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.StockConfigs
            .FirstOrDefaultAsync(c => c.StockCode == config.StockCode, cancellationToken);

        config.UpdatedAt = DateTime.Now;

        if (existing == null)
        {
            config.CreatedAt = DateTime.Now;
            context.StockConfigs.Add(config);
        }
        else
        {
            config.Id = existing.Id;
            context.Entry(existing).CurrentValues.SetValues(config);
        }

        await context.SaveChangesAsync(cancellationToken);
        return config;
    }

    /// <summary>
    /// 删除配置
    /// </summary>
    public async Task<bool> DeleteConfigAsync(string stockCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.StockConfigs
            .FirstOrDefaultAsync(c => c.StockCode == stockCode, cancellationToken);

        if (existing == null)
        {
            return false;
        }

        context.StockConfigs.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 更新状态与触发时间
    /// </summary>
    public async Task UpdateStatusAsync(
        string stockCode,
        StockStatus status,
        DateTime? lastBuyTriggerTime = null,
        DateTime? lastSellTriggerTime = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.StockConfigs
            .FirstOrDefaultAsync(c => c.StockCode == stockCode, cancellationToken);

        if (existing == null)
        {
            return;
        }

        existing.Status = status;
        if (lastBuyTriggerTime.HasValue)
        {
            existing.LastBuyTriggerTime = lastBuyTriggerTime.Value;
        }
        if (lastSellTriggerTime.HasValue)
        {
            existing.LastSellTriggerTime = lastSellTriggerTime.Value;
        }
        existing.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);
    }
}
