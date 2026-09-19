using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Models;

/// <summary>
/// 交易信号
/// </summary>
public record TradeSignal
{
    public TradeSignalType SignalType { get; init; } = TradeSignalType.None;

    public string StockCode { get; init; } = string.Empty;

    public string StockName { get; init; } = string.Empty;

    public decimal CurrentPrice { get; init; }

    public decimal BenchmarkPrice { get; init; }

    public decimal ChangePercent { get; init; }

    public int SuggestedQuantity { get; init; }

    public OrderType OrderType { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTime Timestamp { get; init; } = DateTime.Now;
}
