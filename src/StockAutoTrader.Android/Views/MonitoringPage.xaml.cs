using System.Collections.ObjectModel;
using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.AndroidApp.Views;

/// <summary>
/// 股票监控页面（Android）
/// ContentPage 已继承 BindableObject（含 PropertyChanged 事件），无需再实现 INotifyPropertyChanged
/// </summary>
public partial class MonitoringPage : ContentPage
{
    private bool _isRunning;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

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
        StartCommand = new Command(() => StartMonitoring());
        StopCommand = new Command(() => StopMonitoring());
        InitializeComponent();

        BindingContext = this;
        PositionList.ItemsSource = Positions;
    }

    /// <summary>
    /// 启动监控（预留接口，实际逻辑由 ITradingService 驱动）
    /// </summary>
    private void StartMonitoring()
    {
        IsRunning = true;
        StatusLabel.Text = "运行中";
        // TODO: 调用 ITradingService.StartAsync()
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    private void StopMonitoring()
    {
        IsRunning = false;
        StatusLabel.Text = "已停止";
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
