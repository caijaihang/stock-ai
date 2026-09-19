using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;
using StockAutoTrader.Infrastructure.Providers;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.App.ViewModels;

/// <summary>
/// 主界面视图模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITradingService _tradingService;
    private readonly IStockRepository _stockRepository;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ILoggerService _logger;
    private readonly DispatcherTimer _uiTimer;

    [ObservableProperty]
    private Account _account = new();

    [ObservableProperty]
    private string _statusText = "已停止";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _refreshIntervalSeconds = 3;

    [ObservableProperty]
    private ObservableCollection<StockConfig> _stocks = new();

    [ObservableProperty]
    private ObservableCollection<Position> _positions = new();

    [ObservableProperty]
    private ObservableCollection<Order> _orders = new();

    [ObservableProperty]
    private ObservableCollection<Trade> _trades = new();

    [ObservableProperty]
    private ObservableCollection<TradingLog> _logs = new();

    [ObservableProperty]
    private StockConfig? _selectedStock;

    [ObservableProperty]
    private string _newStockCode = string.Empty;

    [ObservableProperty]
    private string _newStockName = string.Empty;

    [ObservableProperty]
    private decimal _newBuyThreshold = 2.0m;

    [ObservableProperty]
    private decimal _newSellThreshold = 2.0m;

    [ObservableProperty]
    private int _newBuyQuantity = 100;

    [ObservableProperty]
    private int _newSellQuantity = 100;

    public MainViewModel()
    {
        _tradingService = App.ServiceProvider.GetRequiredService<ITradingService>();
        _stockRepository = App.ServiceProvider.GetRequiredService<IStockRepository>();
        _marketDataProvider = App.ServiceProvider.GetRequiredService<IMarketDataProvider>();
        _logger = App.ServiceProvider.GetRequiredService<ILoggerService>();

        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiTimer.Tick += async (_, _) => await RefreshUiAsync();

        _tradingService.OnRefreshed += (_, _) => _ = RefreshUiAsync();

        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        await _tradingService.StartAsync();
        IsRunning = _tradingService.IsRunning;
        StatusText = IsRunning ? "运行中" : "已停止";
        _uiTimer.Start();
        _logger.Info("用户点击启动", "UI");
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _tradingService.StopAsync();
        IsRunning = _tradingService.IsRunning;
        StatusText = "已停止";
        _uiTimer.Stop();
        _logger.Info("用户点击停止", "UI");
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        await _tradingService.StartAllAsync();
        _logger.Info("用户点击全部启动", "UI");
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        await _tradingService.StopAllAsync();
        _logger.Info("用户点击全部停止", "UI");
    }

    [RelayCommand]
    private async Task LiquidateAsync()
    {
        if (MessageBox.Show("确定要一键清仓吗？", "确认", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            await _tradingService.LiquidateAllAsync();
            _logger.Info("用户点击一键清仓", "UI");
        }
    }

    [RelayCommand]
    private async Task AddStockAsync()
    {
        if (string.IsNullOrWhiteSpace(NewStockCode))
        {
            MessageBox.Show("请输入股票代码");
            return;
        }

        var config = new StockConfig
        {
            StockCode = NewStockCode.Trim(),
            StockName = NewStockName.Trim(),
            BuyThresholdPercent = NewBuyThreshold,
            SellThresholdPercent = NewSellThreshold,
            BuyQuantity = NewBuyQuantity,
            SellQuantity = NewSellQuantity,
            Status = StockStatus.Running
        };

        await _stockRepository.SaveConfigAsync(config);

        // 初始化模拟行情
        if (_marketDataProvider is SimulatedMarketDataProvider sim)
        {
            sim.InitializeStock(config.StockCode, config.StockName, 10.0m);
        }

        NewStockCode = string.Empty;
        NewStockName = string.Empty;
        await LoadDataAsync();
        _logger.Info($"添加股票 {config.StockCode}", "UI");
    }

    [RelayCommand]
    private async Task DeleteStockAsync()
    {
        if (SelectedStock == null)
        {
            return;
        }

        await _stockRepository.DeleteConfigAsync(SelectedStock.StockCode);
        await LoadDataAsync();
        _logger.Info($"删除股票 {SelectedStock.StockCode}", "UI");
    }

    [RelayCommand]
    private async Task ToggleStockAsync()
    {
        if (SelectedStock == null)
        {
            return;
        }

        if (SelectedStock.Status == StockStatus.Running)
        {
            await _tradingService.StopStockAsync(SelectedStock.StockCode);
        }
        else
        {
            await _tradingService.StartStockAsync(SelectedStock.StockCode);
        }

        await LoadDataAsync();
    }

    [RelayCommand]
    private void ChangeRefreshInterval()
    {
        // 实际刷新间隔在设置中调整，此处仅做示例
        _logger.Info($"设置刷新间隔为 {RefreshIntervalSeconds} 秒", "UI");
    }

    /// <summary>
    /// 加载数据
    /// </summary>
    private async Task LoadDataAsync()
    {
        var configs = await _stockRepository.GetAllConfigsAsync();
        Stocks = new ObservableCollection<StockConfig>(configs);

        // 初始化模拟行情
        if (_marketDataProvider is SimulatedMarketDataProvider sim)
        {
            foreach (var config in configs)
            {
                sim.InitializeStock(config.StockCode, config.StockName, 10.0m);
            }
        }

        await RefreshUiAsync();
    }

    /// <summary>
    /// 刷新界面数据
    /// </summary>
    private async Task RefreshUiAsync()
    {
        try
        {
            Account = await _tradingService.GetAccountAsync();
            Positions = new ObservableCollection<Position>(await _tradingService.GetPositionsAsync());
            Orders = new ObservableCollection<Order>(await _tradingService.GetOrdersAsync());
            Trades = new ObservableCollection<Trade>(await _tradingService.GetTradesAsync());

            // 刷新股票状态
            var configs = await _stockRepository.GetAllConfigsAsync();
            foreach (var config in configs)
            {
                var existing = Stocks.FirstOrDefault(s => s.StockCode == config.StockCode);
                if (existing != null)
                {
                    existing.Status = config.Status;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("刷新界面失败", ex, "UI");
        }
    }
}
