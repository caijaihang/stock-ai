namespace StockAutoTrader.Core.Enums;

/// <summary>
/// 订单状态
/// </summary>
public enum OrderStatus
{
    /// <summary>待报</summary>
    Pending = 0,

    /// <summary>已报</summary>
    Submitted = 1,

    /// <summary>部分成交</summary>
    PartiallyFilled = 2,

    /// <summary>全部成交</summary>
    Filled = 3,

    /// <summary>已撤单</summary>
    Cancelled = 4,

    /// <summary>撤单中</summary>
    Cancelling = 5,

    /// <summary>失败</summary>
    Rejected = 6,

    /// <summary>过期</summary>
    Expired = 7
}
