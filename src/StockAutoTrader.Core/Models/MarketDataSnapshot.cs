namespace StockAutoTrader.Core.Models;

/// <summary>
/// 行情快照
/// </summary>
public record MarketDataSnapshot
{
    public string StockCode { get; init; } = string.Empty;

    public string StockName { get; init; } = string.Empty;

    public decimal CurrentPrice { get; init; }

    public decimal ChangePercent { get; init; }

    public decimal PreviousClose { get; init; }

    public decimal Open { get; init; }

    public decimal High { get; init; }

    public decimal Low { get; init; }

    public long Volume { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.Now;

    public bool IsValid => CurrentPrice > 0 && Timestamp != default;
}
