using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Models;

/// <summary>
/// 策略参数（不可变配置）
/// </summary>
public record StrategyParameters
{
    public string StockCode { get; init; } = string.Empty;

    public string StockName { get; init; } = string.Empty;

    public MarketType Market { get; init; }

    public BenchmarkPriceType BenchmarkPriceType { get; init; }

    public decimal CustomBenchmarkPrice { get; init; }

    public decimal BuyThresholdPercent { get; init; }

    public decimal SellThresholdPercent { get; init; }

    public int BuyQuantity { get; init; }

    public int SellQuantity { get; init; }

    public int CooldownSeconds { get; init; }

    public int MaxBuyTimesPerDay { get; init; }

    public int MaxSellTimesPerDay { get; init; }

    public string TradingHours { get; init; } = "09:30-11:30,13:00-15:00";

    public bool T1Enabled { get; init; }

    public bool AllowRepeatBuy { get; init; }

    public bool AllowPartialSell { get; init; }

    public OrderType OrderType { get; init; }
}
