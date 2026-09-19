namespace StockAutoTrader.Core.Enums;

/// <summary>
/// 订单类型
/// </summary>
public enum OrderType
{
    /// <summary>市价单</summary>
    Market = 1,

    /// <summary>限价单</summary>
    Limit = 2,

    /// <summary>模拟即时成交</summary>
    SimulatedImmediate = 3
}
