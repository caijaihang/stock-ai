using Microsoft.EntityFrameworkCore;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Infrastructure.Data;

/// <summary>
/// 账户管理 EF 实现
/// </summary>
public class EfAccountManager : IAccountManager
{
    private readonly IDbContextFactory<TradingDbContext> _contextFactory;

    public EfAccountManager(IDbContextFactory<TradingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// 获取账户信息
    /// </summary>
    public async Task<Account> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var account = await context.Accounts.FirstOrDefaultAsync(cancellationToken);
        if (account == null)
        {
            account = new Account
            {
                InitialCapital = 1000000m,
                AvailableCash = 1000000m,
                AccountName = "模拟账户"
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync(cancellationToken);
        }

        return account;
    }

    /// <summary>
    /// 初始化账户资金
    /// </summary>
    public async Task<Account> InitializeAsync(decimal initialCapital, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var account = await context.Accounts.FirstOrDefaultAsync(cancellationToken);
        if (account == null)
        {
            account = new Account
            {
                InitialCapital = initialCapital,
                AvailableCash = initialCapital,
                AccountName = "模拟账户"
            };
            context.Accounts.Add(account);
        }
        else
        {
            account.InitialCapital = initialCapital;
            account.AvailableCash = initialCapital;
            account.TotalMarketValue = 0;
            account.TotalAssets = initialCapital;
            account.TotalPnl = 0;
            account.TotalPnlPercent = 0;
        }

        account.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        return account;
    }

    /// <summary>
    /// 买入扣款
    /// </summary>
    public async Task<bool> DeductForBuyAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var account = await context.Accounts.FirstOrDefaultAsync(cancellationToken);
        if (account == null || account.AvailableCash < amount)
        {
            return false;
        }

        account.AvailableCash -= amount;
        account.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 卖出回款
    /// </summary>
    public async Task<bool> RefundForSellAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var account = await context.Accounts.FirstOrDefaultAsync(cancellationToken);
        if (account == null)
        {
            return false;
        }

        account.AvailableCash += amount;
        account.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 更新总市值
    /// </summary>
    public async Task UpdateMarketValueAsync(decimal marketValue, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var account = await context.Accounts.FirstOrDefaultAsync(cancellationToken);
        if (account == null)
        {
            return;
        }

        account.TotalMarketValue = marketValue;
        account.TotalAssets = account.AvailableCash + marketValue;
        account.TotalPnl = account.TotalAssets - account.InitialCapital;
        account.TotalPnlPercent = account.InitialCapital > 0
            ? account.TotalPnl / account.InitialCapital * 100m
            : 0;
        account.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
    }
}
