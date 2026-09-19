using CommunityToolkit.Mvvm.ComponentModel;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Entities;

/// <summary>
/// 成交记录
/// </summary>
public partial class Trade : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _tradeId = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private string _stockCode = string.Empty;

    [ObservableProperty]
    private string _stockName = string.Empty;

    [ObservableProperty]
    private OrderSide _side;

    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private decimal _commission;

    [ObservableProperty]
    private DateTime _tradeTime = DateTime.Now;

    [ObservableProperty]
    private string _remark = string.Empty;
}
