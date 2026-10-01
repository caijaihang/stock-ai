namespace StockAutoTrader.Core.Models;

/// <summary>
/// K 线记录（日线/分钟线通用）
/// </summary>
public class KlineRecord
{
    /// <summary>时间（日线为日期 00:00:00，分钟线为具体分钟）</summary>
    public DateTime DateTime { get; set; }

    /// <summary>开盘价</summary>
    public decimal Open { get; set; }

    /// <summary>最高价</summary>
    public decimal High { get; set; }

    /// <summary>最低价</summary>
    public decimal Low { get; set; }

    /// <summary>收盘价</summary>
    public decimal Close { get; set; }

    /// <summary>成交量（手）</summary>
    public long Volume { get; set; }

    /// <summary>成交额</summary>
    public decimal Amount { get; set; }
}
