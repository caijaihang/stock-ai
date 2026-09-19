using CommunityToolkit.Mvvm.ComponentModel;

namespace StockAutoTrader.Core.Entities;

/// <summary>
/// 交易日志
/// </summary>
public partial class TradingLog : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private string _level = "Info";

    [ObservableProperty]
    private string _category = "General";

    [ObservableProperty]
    private string _stockCode = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string? _exception;
}
