using CommunityToolkit.Mvvm.ComponentModel;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.Core.Entities;

/// <summary>
/// 委托订单
/// </summary>
public partial class Order : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _orderId = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _stockCode = string.Empty;

    [ObservableProperty]
    private string _stockName = string.Empty;

    [ObservableProperty]
    private OrderSide _side;

    [ObservableProperty]
    private OrderType _orderType;

    [ObservableProperty]
    private OrderStatus _status = OrderStatus.Pending;

    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private int _filledQuantity;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private decimal _filledPrice;

    [ObservableProperty]
    private decimal _commission;

    [ObservableProperty]
    private string _strategyTrigger = string.Empty;

    [ObservableProperty]
    private DateTime _orderTime = DateTime.Now;

    [ObservableProperty]
    private DateTime? _fillTime;

    [ObservableProperty]
    private string _remark = string.Empty;
}
