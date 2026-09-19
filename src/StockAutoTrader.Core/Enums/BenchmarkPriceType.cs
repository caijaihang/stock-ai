namespace StockAutoTrader.Core.Enums;

/// <summary>
/// 基准价类型
/// </summary>
public enum BenchmarkPriceType
{
    /// <summary>昨收价</summary>
    PreviousClose = 0,

    /// <summary>开盘价</summary>
    Open = 1,

    /// <summary>最新价</summary>
    Latest = 2,

    /// <summary>持仓成本</summary>
    PositionCost = 3,

    /// <summary>自定义价</summary>
    Custom = 4
}
