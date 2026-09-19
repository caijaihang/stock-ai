using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.Android.Views;

/// <summary>
/// 股票监控页面（Android）
/// </summary>
[ObservableProperty]
public partial class MonitoringPage : ContentPage
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// 启动监控命令
    /// </summary>
    public Command StartCommand { get; }

    /// <summary>
    /// 停止监控命令
    /// </summary>
    public Command StopCommand { get; }

    /// <summary>
    /// 持仓列表
    /// </summary>
    public ObservableCollection<Position> Positions { get; } = new();

    public MonitoringPage()
    {
        StartCommand = new Command(() => StartMonitoringAsync());
        StopCommand = new Command(() => StopMonitoring());
        InitializeComponent();

        BindingContext = this;
        PositionList.ItemsSource = Positions;
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IsRunning))
            {
                StatusLabel.Text = IsRunning ? "运行中" : "已停止";
            }
        };
    }

    /// <summary>
    /// 启动监控（预留接口，实际逻辑由 ITradingService 驱动）
    /// </summary>
    private void StartMonitoringAsync()
    {
        IsRunning = true;
        // TODO: 调用 ITradingService.StartAsync()
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    private void StopMonitoring()
    {
        IsRunning = false;
        // TODO: 调用 ITradingService.StopAsync()
    }

    /// <summary>
    /// 刷新持仓显示
    /// </summary>
    public void RefreshPositions(IReadOnlyList<Position> positions)
    {
        Positions.Clear();
        foreach (var p in positions)
        {
            Positions.Add(p);
        }
    }

    /// <summary>
    /// 刷新账户显示
    /// </summary>
    public void RefreshAccount(decimal balance, decimal marketValue)
    {
        BalanceLabel.Text = balance.ToString("C");
        MarketValueLabel.Text = marketValue.ToString("C");
    }
}
