using CommunityToolkit.Mvvm.ComponentModel;

namespace StockAutoTrader.Core.Entities;

/// <summary>
/// 模拟账户资金
/// </summary>
public partial class Account : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _accountName = "模拟账户";

    [ObservableProperty]
    private decimal _initialCapital;

    [ObservableProperty]
    private decimal _availableCash;

    [ObservableProperty]
    private decimal _totalMarketValue;

    [ObservableProperty]
    private decimal _totalAssets;

    [ObservableProperty]
    private decimal _totalPnl;

    [ObservableProperty]
    private decimal _totalPnlPercent;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;
}
