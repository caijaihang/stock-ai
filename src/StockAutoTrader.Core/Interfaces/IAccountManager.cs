using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 账户管理接口
/// </summary>
public interface IAccountManager
{
    /// <summary>
    /// 获取账户信息
    /// </summary>
    Task<Account> GetAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 初始化账户资金
    /// </summary>
    Task<Account> InitializeAsync(decimal initialCapital, CancellationToken cancellationToken = default);

    /// <summary>
    /// 买入扣款
    /// </summary>
    Task<bool> DeductForBuyAsync(decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 卖出回款
    /// </summary>
    Task<bool> RefundForSellAsync(decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新总市值
    /// </summary>
    Task UpdateMarketValueAsync(decimal marketValue, CancellationToken cancellationToken = default);
}
