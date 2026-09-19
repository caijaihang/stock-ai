namespace StockAutoTrader.Core.Enums;

/// <summary>
/// 股票市场类型
/// </summary>
public enum MarketType
{
    /// <summary>未知市场</summary>
    Unknown = 0,

    /// <summary>上海证券交易所</summary>
    Shanghai = 1,

    /// <summary>深圳证券交易所</summary>
    Shenzhen = 2,

    /// <summary>北京证券交易所</summary>
    Beijing = 3,

    /// <summary>港股</summary>
    HongKong = 4
}
