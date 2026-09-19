using CommunityToolkit.Mvvm.ComponentModel;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Entities;

/// <summary>
/// 股票策略配置
/// </summary>
public partial class StockConfig : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _stockCode = string.Empty;

    [ObservableProperty]
    private string _stockName = string.Empty;

    [ObservableProperty]
    private MarketType _market = MarketType.Shanghai;

    [ObservableProperty]
    private BenchmarkPriceType _benchmarkPriceType = BenchmarkPriceType.PreviousClose;

    [ObservableProperty]
    private decimal _customBenchmarkPrice;

    [ObservableProperty]
    private decimal _buyThresholdPercent;

    [ObservableProperty]
    private decimal _sellThresholdPercent;

    [ObservableProperty]
    private int _buyQuantity;

    [ObservableProperty]
    private int _sellQuantity;

    [ObservableProperty]
    private int _cooldownSeconds = 60;

    [ObservableProperty]
    private int _maxBuyTimesPerDay = 3;

    [ObservableProperty]
    private int _maxSellTimesPerDay = 3;

    [ObservableProperty]
    private string _tradingHours = "09:30-11:30,13:00-15:00";

    [ObservableProperty]
    private bool _t1Enabled = true;

    [ObservableProperty]
    private bool _allowRepeatBuy = true;

    [ObservableProperty]
    private bool _allowPartialSell = true;

    [ObservableProperty]
    private OrderType _orderType = OrderType.SimulatedImmediate;

    [ObservableProperty]
    private StockStatus _status = StockStatus.Running;

    [ObservableProperty]
    private DateTime _lastBuyTriggerTime = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _lastSellTriggerTime = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;
}
