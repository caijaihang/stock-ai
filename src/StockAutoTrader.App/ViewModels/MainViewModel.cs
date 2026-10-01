using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Enums;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Core.Models;
using StockAutoTrader.Infrastructure.Providers;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.App.ViewModels;

/// <summary>
/// 主界面视图模型（完整功能：实时行情列、编辑、设置、导入导出、暂停、筛选、撤单）
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITradingService _tradingService;
    private readonly IStockRepository _stockRepository;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ILoggerService _logger;
    private readonly ServiceSettings _settings;
    private readonly DispatcherTimer _uiTimer;

    [ObservableProperty] private Account _account = new();
    [ObservableProperty] private string _statusText = "已停止";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private int _refreshIntervalSeconds = 3;
    [ObservableProperty] private ObservableCollection<StockRow> _stocks = new();
    [ObservableProperty] private ObservableCollection<Position> _positions = new();
    [ObservableProperty] private ObservableCollection<Order> _orders = new();
    [ObservableProperty] private ObservableCollection<Trade> _trades = new();
    [ObservableProperty] private ObservableCollection<TradingLog> _logs = new();
    [ObservableProperty] private StockRow? _selectedStock;
    [ObservableProperty] private Order? _selectedOrder;
    [ObservableProperty] private Position? _selectedPosition;
    [ObservableProperty] private ISeries[] _priceChartSeries = Array.Empty<ISeries>();

    // 添加股票输入
    [ObservableProperty] private string _newStockCode = string.Empty;
    [ObservableProperty] private string _newStockName = string.Empty;
    [ObservableProperty] private MarketType _newMarket = MarketType.Shanghai;
    [ObservableProperty] private BenchmarkPriceType _newBenchmarkType = BenchmarkPriceType.PreviousClose;
    [ObservableProperty] private decimal _newCustomBenchmarkPrice;
    [ObservableProperty] private decimal _newBuyThreshold = 2.0m;
    [ObservableProperty] private decimal _newSellThreshold = 2.0m;
    [ObservableProperty] private int _newBuyQuantity = 100;
    [ObservableProperty] private int _newSellQuantity = 100;
    [ObservableProperty] private int _newCooldownSeconds = 60;

    // 日志筛选
    [ObservableProperty] private string _logFilterStock = string.Empty;
    [ObservableProperty] private string _logFilterCategory = string.Empty;
    [ObservableProperty] private string _logFilterLevel = string.Empty;

    // 设置
    [ObservableProperty] private string _settingsMarketProvider = "Simulated";
    [ObservableProperty] private string _settingsTradeExecutor = "Simulated";
    [ObservableProperty] private decimal _settingsCommissionRate = 0.0003m;
    [ObservableProperty] private decimal _settingsSlippage;
    [ObservableProperty] private bool _settingsT1Enabled = true;
    [ObservableProperty] private bool _settingsSoundNotification = true;
    [ObservableProperty] private bool _settingsPopupNotification = true;
    [ObservableProperty] private bool _settingsSystemToastNotification;
    [ObservableProperty] private decimal _settingsInitialCapital = 1000000m;
    [ObservableProperty] private string _settingsTongDaXinExePath = string.Empty;
    [ObservableProperty] private string _settingsTongHuaShunExePath = string.Empty;
    [ObservableProperty] private string _settingsAiStockApiUrl = string.Empty;
    [ObservableProperty] private string _settingsAiStockApiKey = string.Empty;

    public Array MarketTypes => Enum.GetValues(typeof(MarketType));
    public Array BenchmarkTypes => Enum.GetValues(typeof(BenchmarkPriceType));
    public Array OrderTypes => Enum.GetValues(typeof(OrderType));
    public Array RefreshIntervals => new[] { 1, 3, 5, 10 };

    public MainViewModel()
    {
        _tradingService = App.ServiceProvider.GetRequiredService<ITradingService>();
        _stockRepository = App.ServiceProvider.GetRequiredService<IStockRepository>();
        _marketDataProvider = App.ServiceProvider.GetRequiredService<IMarketDataProvider>();
        _logger = App.ServiceProvider.GetRequiredService<ILoggerService>();
        _settings = App.ServiceProvider.GetRequiredService<ServiceSettings>();

        LoadSettingsToUi();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += async (_, _) => await RefreshUiAsync();
        _tradingService.OnRefreshed += (_, _) => _ = RefreshUiAsync();
        _ = LoadDataAsync();
    }

    private void LoadSettingsToUi()
    {
        SettingsMarketProvider = _settings.MarketDataProvider;
        SettingsTradeExecutor = _settings.TradeExecutor;
        SettingsCommissionRate = _settings.CommissionRate;
        SettingsSlippage = _settings.Slippage;
        SettingsT1Enabled = _settings.T1Enabled;
        SettingsSoundNotification = _settings.SoundNotification;
        SettingsPopupNotification = _settings.PopupNotification;
        SettingsSystemToastNotification = _settings.SystemToastNotification;
        SettingsInitialCapital = _settings.InitialCapital;
        SettingsTongDaXinExePath = _settings.TongDaXinExePath;
        SettingsTongHuaShunExePath = _settings.TongHuaShunExePath;
        SettingsAiStockApiUrl = _settings.AiStockApiUrl;
        SettingsAiStockApiKey = _settings.AiStockApiKey;
        RefreshIntervalSeconds = _settings.RefreshIntervalMs / 1000;
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
        IsRunning = false; IsPaused = false;
        StatusText = "已停止";
        _uiTimer.Stop();
        _logger.Info("用户点击停止", "UI");
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        await _tradingService.PauseAllAsync();
        IsPaused = true;
        StatusText = "已暂停";
        _logger.Info("用户点击一键暂停", "UI");
    }

    [RelayCommand]
    private async Task ResumeAsync()
    {
        await _tradingService.ResumeAllAsync();
        IsPaused = false;
        StatusText = IsRunning ? "运行中" : "已停止";
        _logger.Info("用户点击恢复", "UI");
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        await _tradingService.StartAllAsync();
        _logger.Info("全部启动", "UI");
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        await _tradingService.StopAllAsync();
        _logger.Info("全部停止", "UI");
    }

    [RelayCommand]
    private async Task LiquidateAsync()
    {
        if (MessageBox.Show("确定要一键清仓吗？", "确认", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            await _tradingService.LiquidateAllAsync();
            _logger.Info("一键清仓", "UI");
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
            Market = NewMarket,
            BenchmarkPriceType = NewBenchmarkType,
            CustomBenchmarkPrice = NewCustomBenchmarkPrice,
            BuyThresholdPercent = NewBuyThreshold,
            SellThresholdPercent = NewSellThreshold,
            BuyQuantity = NewBuyQuantity,
            SellQuantity = NewSellQuantity,
            CooldownSeconds = NewCooldownSeconds,
            Status = StockStatus.Running
        };
        await _stockRepository.SaveConfigAsync(config);
        if (_marketDataProvider is SimulatedMarketDataProvider sim)
            sim.InitializeStock(config.StockCode, config.StockName, 10.0m);
        NewStockCode = string.Empty; NewStockName = string.Empty;
        await LoadDataAsync();
        _logger.Info($"添加股票 {config.StockCode}", "UI");
    }

    [RelayCommand]
    private async Task DeleteStockAsync()
    {
        if (SelectedStock == null) return;
        await _stockRepository.DeleteConfigAsync(SelectedStock.StockCode);
        await LoadDataAsync();
        _logger.Info($"删除股票 {SelectedStock.StockCode}", "UI");
    }

    [RelayCommand]
    private async Task ToggleStockAsync()
    {
        if (SelectedStock == null) return;
        if (SelectedStock.Status == StockStatus.Running)
            await _tradingService.StopStockAsync(SelectedStock.StockCode);
        else
            await _tradingService.StartStockAsync(SelectedStock.StockCode);
        await LoadDataAsync();
    }

    [RelayCommand]
    private void EditStock()
    {
        if (SelectedStock == null) return;
        var cfg = SelectedStock.Config;
        var dlg = new EditStockDialog(cfg);
        if (dlg.ShowDialog() == true)
        {
            _stockRepository.SaveConfigAsync(cfg).GetAwaiter().GetResult();
            _ = LoadDataAsync();
            _logger.Info($"编辑股票 {cfg.StockCode}", "UI");
        }
    }

    [RelayCommand]
    private void ChangeRefreshInterval()
    {
        _tradingService.SetRefreshInterval(RefreshIntervalSeconds * 1000);
        _logger.Info($"设置刷新间隔为 {RefreshIntervalSeconds} 秒", "UI");
    }

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (SelectedOrder == null) return;
        if (SelectedOrder.Status != OrderStatus.Pending)
        {
            MessageBox.Show("只有待成交订单可撤单");
            return;
        }
        var ok = await _tradingService.CancelOrderAsync(SelectedOrder.OrderId);
        MessageBox.Show(ok ? "撤单成功" : "撤单失败");
        await RefreshUiAsync();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.MarketDataProvider = SettingsMarketProvider;
        _settings.TradeExecutor = SettingsTradeExecutor;
        _settings.CommissionRate = SettingsCommissionRate;
        _settings.Slippage = SettingsSlippage;
        _settings.T1Enabled = SettingsT1Enabled;
        _settings.SoundNotification = SettingsSoundNotification;
        _settings.PopupNotification = SettingsPopupNotification;
        _settings.SystemToastNotification = SettingsSystemToastNotification;
        _settings.InitialCapital = SettingsInitialCapital;
        _settings.TongDaXinExePath = SettingsTongDaXinExePath;
        _settings.TongHuaShunExePath = SettingsTongHuaShunExePath;
        _settings.AiStockApiUrl = SettingsAiStockApiUrl;
        _settings.AiStockApiKey = SettingsAiStockApiKey;
        _settings.RefreshIntervalMs = RefreshIntervalSeconds * 1000;

        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StockAutoTrader.Core.PortablePathHelper.GetConfigPath(), json);
        MessageBox.Show("设置已保存，重启程序后生效");
        _logger.Info("保存设置", "UI");
    }

    [RelayCommand]
    private void ResetSettings()
    {
        var def = new ServiceSettings();
        var json = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StockAutoTrader.Core.PortablePathHelper.GetConfigPath(), json);
        LoadSettingsToUi();
        MessageBox.Show("已恢复默认设置，重启程序后生效");
    }

    [RelayCommand]
    private void ExportConfig()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON 文件|*.json",
            FileName = $"stock_config_{DateTime.Now:yyyyMMdd}.json"
        };
        if (dlg.ShowDialog() != true) return;
        var configs = _stockRepository.GetAllConfigsAsync().GetAwaiter().GetResult();
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true }));
        MessageBox.Show("配置已导出");
    }

    [RelayCommand]
    private void ImportConfig()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON 文件|*.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var configs = JsonSerializer.Deserialize<List<StockConfig>>(json);
            if (configs == null) return;
            foreach (var cfg in configs)
                _stockRepository.SaveConfigAsync(cfg).GetAwaiter().GetResult();
            _ = LoadDataAsync();
            MessageBox.Show($"已导入 {configs.Count} 条配置");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导入失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task FilterLogsAsync()
    {
        var logs = await _tradingService.GetLogsAsync(
            string.IsNullOrWhiteSpace(LogFilterLevel) ? null : LogFilterLevel,
            string.IsNullOrWhiteSpace(LogFilterCategory) ? null : LogFilterCategory,
            string.IsNullOrWhiteSpace(LogFilterStock) ? null : LogFilterStock);
        Logs = new ObservableCollection<TradingLog>(logs);
    }

    [RelayCommand]
    private void LaunchTongDaXin()
    {
        if (string.IsNullOrWhiteSpace(_settings.TongDaXinExePath) || !File.Exists(_settings.TongDaXinExePath))
        {
            MessageBox.Show("请先在设置页配置通达信可执行文件路径");
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_settings.TongDaXinExePath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void LaunchTongHuaShun()
    {
        if (string.IsNullOrWhiteSpace(_settings.TongHuaShunExePath) || !File.Exists(_settings.TongHuaShunExePath))
        {
            MessageBox.Show("请先在设置页配置同花顺可执行文件路径");
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_settings.TongHuaShunExePath) { UseShellExecute = true });
    }

    private async Task LoadDataAsync()
    {
        var configs = await _stockRepository.GetAllConfigsAsync();
        var positions = await _tradingService.GetPositionsAsync();
        var posMap = positions.ToDictionary(p => p.StockCode, p => p);
        var snapshots = await _marketDataProvider.GetSnapshotsAsync(configs.Select(c => c.StockCode));
        var snapMap = snapshots.ToDictionary(s => s.StockCode, s => s);

        var rows = new ObservableCollection<StockRow>();
        foreach (var cfg in configs)
        {
            var row = new StockRow { Config = cfg };
            if (snapMap.TryGetValue(cfg.StockCode, out var snap))
            {
                row.CurrentPrice = snap.CurrentPrice;
                row.ChangePercent = snap.ChangePercent;
                row.BenchmarkPrice = cfg.BenchmarkPriceType switch
                {
                    BenchmarkPriceType.PreviousClose => snap.PreviousClose,
                    BenchmarkPriceType.Open => snap.Open,
                    BenchmarkPriceType.Latest => snap.CurrentPrice,
                    BenchmarkPriceType.PositionCost => posMap.TryGetValue(cfg.StockCode, out var p) ? p.AverageCost : 0,
                    BenchmarkPriceType.Custom => cfg.CustomBenchmarkPrice,
                    _ => snap.PreviousClose
                };
            }
            if (posMap.TryGetValue(cfg.StockCode, out var pos))
            {
                row.HoldingQuantity = pos.TotalQuantity;
                row.AvailableQuantity = pos.AvailableQuantity;
                row.CostPrice = pos.AverageCost;
                row.MarketValue = pos.MarketValue;
                row.UnrealizedPnl = pos.UnrealizedPnl;
                row.UnrealizedPnlPercent = pos.UnrealizedPnlPercent;
            }
            rows.Add(row);
        }
        Stocks = rows;

        if (_marketDataProvider is SimulatedMarketDataProvider sim)
            foreach (var cfg in configs)
                sim.InitializeStock(cfg.StockCode, cfg.StockName, 10.0m);

        await RefreshUiAsync();
    }

    private async Task RefreshUiAsync()
    {
        try
        {
            Account = await _tradingService.GetAccountAsync();
            Positions = new ObservableCollection<Position>(await _tradingService.GetPositionsAsync());
            Orders = new ObservableCollection<Order>(await _tradingService.GetOrdersAsync());
            Trades = new ObservableCollection<Trade>(await _tradingService.GetTradesAsync());
            Logs = new ObservableCollection<TradingLog>(await _tradingService.GetLogsAsync());

            var configs = await _stockRepository.GetAllConfigsAsync();
            var snapshots = await _marketDataProvider.GetSnapshotsAsync(configs.Select(c => c.StockCode));
            var snapMap = snapshots.ToDictionary(s => s.StockCode, s => s);
            var positions = await _tradingService.GetPositionsAsync();
            var posMap = positions.ToDictionary(p => p.StockCode, p => p);

            foreach (var row in Stocks)
            {
                if (snapMap.TryGetValue(row.StockCode, out var snap))
                {
                    row.CurrentPrice = snap.CurrentPrice;
                    row.ChangePercent = snap.ChangePercent;
                }
                if (posMap.TryGetValue(row.StockCode, out var pos))
                {
                    row.HoldingQuantity = pos.TotalQuantity;
                    row.AvailableQuantity = pos.AvailableQuantity;
                    row.CostPrice = pos.AverageCost;
                    row.MarketValue = pos.MarketValue;
                    row.UnrealizedPnl = pos.UnrealizedPnl;
                    row.UnrealizedPnlPercent = pos.UnrealizedPnlPercent;
                }
            }
        }
        catch (Exception ex) { _logger.Error("刷新界面失败", ex, "UI"); }
    }

    /// <summary>
    /// 选中持仓变化时更新价格走势图
    /// </summary>
    partial void OnSelectedPositionChanged(Position? value)
    {
        UpdatePriceChart(value);
    }

    /// <summary>
    /// 构建价格走势图（从持仓最近的成交价 + 模拟近 30 个点）
    /// </summary>
    private void UpdatePriceChart(Position? pos)
    {
        if (pos == null || pos.AverageCost <= 0)
        {
            PriceChartSeries = Array.Empty<ISeries>();
            return;
        }

        // 以成本价为基准生成 30 个价格点的走势（实际可从行情历史读取）
        var basePrice = pos.AverageCost;
        var values = new List<double>();
        var rnd = new Random(pos.StockCode.GetHashCode());
        var price = (double)basePrice;
        for (var i = 0; i < 30; i++)
        {
            price += rnd.NextDouble() * (double)basePrice * 0.01 - (double)basePrice * 0.005;
            values.Add(Math.Round(price, 2));
        }
        values.Add((double)pos.CurrentPrice); // 最后一点为当前价

        PriceChartSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = pos.StockName,
                Values = values,
                GeometrySize = 4,
                LineSmoothness = 0.5
            }
        };
    }
}
