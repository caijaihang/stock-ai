using CommunityToolkit.Mvvm.ComponentModel;

namespace StockAutoTrader.Core.Entities;

/// <summary>
/// 持仓记录
/// </summary>
public partial class Position : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _stockCode = string.Empty;

    [ObservableProperty]
    private string _stockName = string.Empty;

    [ObservableProperty]
    private int _totalQuantity;

    [ObservableProperty]
    private int _availableQuantity;

    [ObservableProperty]
    private decimal _averageCost;

    [ObservableProperty]
    private decimal _currentPrice;

    [ObservableProperty]
    private decimal _marketValue;

    [ObservableProperty]
    private decimal _unrealizedPnl;

    [ObservableProperty]
    private decimal _unrealizedPnlPercent;

    [ObservableProperty]
    private DateTime _lastBuyDate = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;
}
