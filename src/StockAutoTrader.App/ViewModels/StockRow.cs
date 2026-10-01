using CommunityToolkit.Mvvm.ComponentModel;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.App.ViewModels;

/// <summary>
/// 股票监控列表行（合并配置 + 实时行情 + 持仓）
/// </summary>
public partial class StockRow : ObservableObject
{
    [ObservableProperty]
    private StockConfig _config = new();

    [ObservableProperty]
    private decimal _currentPrice;

    [ObservableProperty]
    private decimal _changePercent;

    [ObservableProperty]
    private decimal _benchmarkPrice;

    [ObservableProperty]
    private int _holdingQuantity;

    [ObservableProperty]
    private int _availableQuantity;

    [ObservableProperty]
    private decimal _costPrice;

    [ObservableProperty]
    private decimal _marketValue;

    [ObservableProperty]
    private decimal _unrealizedPnl;

    [ObservableProperty]
    private decimal _unrealizedPnlPercent;

    public string StockCode => Config.StockCode;
    public string StockName => Config.StockName;
    public MarketType Market => Config.Market;
    public BenchmarkPriceType BenchmarkPriceType => Config.BenchmarkPriceType;
    public decimal BuyThresholdPercent => Config.BuyThresholdPercent;
    public decimal SellThresholdPercent => Config.SellThresholdPercent;
    public int BuyQuantity => Config.BuyQuantity;
    public int SellQuantity => Config.SellQuantity;
    public int CooldownSeconds => Config.CooldownSeconds;
    public int MaxBuyTimesPerDay => Config.MaxBuyTimesPerDay;
    public int MaxSellTimesPerDay => Config.MaxSellTimesPerDay;
    public string TradingHours => Config.TradingHours;
    public bool T1Enabled => Config.T1Enabled;
    public OrderType OrderType => Config.OrderType;
    public StockStatus Status => Config.Status;
    public DateTime LastBuyTriggerTime => Config.LastBuyTriggerTime;
    public DateTime LastSellTriggerTime => Config.LastSellTriggerTime;
}
