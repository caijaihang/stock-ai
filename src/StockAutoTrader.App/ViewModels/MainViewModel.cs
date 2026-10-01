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
    private readonly AiStockSelector _aiStockSelector;
    private readonly TradingDiaryService _diaryService;
    private readonly StrategySquareService _strategyService;
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

    // AI 选股
    [ObservableProperty] private string _aiCondition = string.Empty;
    [ObservableProperty] private string _aiMarket = "all";
    [ObservableProperty] private int _aiMaxCount = 50;
    [ObservableProperty] private ObservableCollection<StockRow> _aiResults = new();
    [ObservableProperty] private StockRow? _selectedAiResult;

    // 交易日记
    [ObservableProperty] private string _diaryTitle = string.Empty;
    [ObservableProperty] private string _diaryContent = string.Empty;
    [ObservableProperty] private string _diaryType = "note";
    [ObservableProperty] private string _diaryStockCode = string.Empty;
    [ObservableProperty] private ObservableCollection<TradingDiaryEntry> _diaryEntries = new();
    [ObservableProperty] private TradingDiaryEntry? _selectedDiaryEntry;

    // 策略广场
    [ObservableProperty] private ObservableCollection<StrategyDefinition> _strategies = new();
    [ObservableProperty] private StrategyDefinition? _selectedStrategy;

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
    [ObservableProperty] private string _settingsWebSocketServerUrl = string.Empty;
    [ObservableProperty] private string _settingsWebSocketApiKey = string.Empty;
    [ObservableProperty] private string _settingsWebhookUrl = string.Empty;
    [ObservableProperty] private string _settingsWebhookType = "feishu";

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
        _aiStockSelector = App.ServiceProvider.GetRequiredService<AiStockSelector>();
        _diaryService = App.ServiceProvider.GetRequiredService<TradingDiaryService>();
        _strategyService = App.ServiceProvider.GetRequiredService<StrategySquareService>();

        LoadSettingsToUi();
        LoadDiary();
        LoadStrategies();

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
        SettingsWebSocketServerUrl = _settings.WebSocketServerUrl;
        SettingsWebSocketApiKey = _settings.WebSocketApiKey;
        SettingsWebhookUrl = _settings.WebhookUrl;
        SettingsWebhookType = _settings.WebhookType;
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

    /// <summary>
    /// 从通达信自选股板块导入股票
    /// </summary>
    [RelayCommand]
    private async Task ImportWatchlistAsync()
    {
        try
        {
            var tdxDir = _settings.TdxInstallDirectory;
            if (string.IsNullOrWhiteSpace(tdxDir))
            {
                MessageBox.Show("请先在设置页配置通达信安装目录");
                return;
            }

            var codes = StockAutoTrader.Infrastructure.Providers.TdxBlockNewReader.ReadBlock(tdxDir);
            if (codes.Count == 0)
            {
                MessageBox.Show("未读取到自选股，请确认通达信目录下 T0002/blocknew/ 存在自选股文件");
                return;
            }

            var imported = 0;
            foreach (var code in codes)
            {
                var existing = await _stockRepository.GetConfigAsync(code);
                if (existing != null) continue; // 已存在则跳过

                var market = code.StartsWith("6") ? MarketType.Shanghai : MarketType.Shenzhen;
                var cfg = new StockConfig
                {
                    StockCode = code,
                    StockName = code,
                    Market = market,
                    BenchmarkPriceType = BenchmarkPriceType.PreviousClose,
                    BuyThresholdPercent = 2.0m,
                    SellThresholdPercent = 2.0m,
                    BuyQuantity = 100,
                    SellQuantity = 100,
                    CooldownSeconds = 60,
                    MaxBuyTimesPerDay = 1,
                    MaxSellTimesPerDay = 1,
                    Status = StockStatus.Stopped,
                    TradingHours = "09:30-11:30,13:00-15:00"
                };
                await _stockRepository.SaveConfigAsync(cfg);
                imported++;
            }

            MessageBox.Show($"成功导入 {imported} 只自选股（跳过已存在的 {codes.Count - imported} 只）");
            _logger.Info($"从通达信导入自选股 {imported} 只", "UI");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 运行 AI 选股
    /// </summary>
    [RelayCommand]
    private async Task RunAiSelectionAsync()
    {
        if (string.IsNullOrWhiteSpace(AiCondition))
        {
            MessageBox.Show("请输入选股条件");
            return;
        }

        StatusText = "AI 选股中...";
        try
        {
            var results = await _aiStockSelector.SelectAsync(AiCondition, AiMarket, AiMaxCount);
            AiResults.Clear();
            if (results.Count == 0)
            {
                MessageBox.Show("未找到符合条件的股票，或 AI 接口未配置/不可用");
            }
            else
            {
                // 补充行情数据
                var snapshots = await _marketDataProvider.GetSnapshotsAsync(results.Select(r => r.StockCode).ToList());
                var snapMap = snapshots.ToDictionary(s => s.StockCode);

                foreach (var r in results)
                {
                    var row = new StockRow { Config = new StockConfig { StockCode = r.StockCode, StockName = r.StockName } };
                    if (snapMap.TryGetValue(r.StockCode, out var snap))
                    {
                        row.CurrentPrice = snap.CurrentPrice;
                        row.ChangePercent = snap.ChangePercent;
                    }
                    AiResults.Add(row);
                }
            }
            _logger.Info($"AI 选股完成，返回 {results.Count} 只", "AI");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"AI 选股失败：{ex.Message}");
        }
        finally
        {
            StatusText = IsRunning ? "运行中" : "已停止";
        }
    }

    /// <summary>
    /// 将 AI 选股结果全部添加到监控列表
    /// </summary>
    [RelayCommand]
    private async Task AddAiResultsToWatchAsync()
    {
        if (AiResults.Count == 0)
        {
            MessageBox.Show("没有可添加的选股结果");
            return;
        }

        var added = 0;
        foreach (var row in AiResults)
        {
            var code = row.Config.StockCode;
            var existing = await _stockRepository.GetConfigAsync(code);
            if (existing != null) continue;

            var market = code.StartsWith("6") ? MarketType.Shanghai : MarketType.Shenzhen;
            var cfg = new StockConfig
            {
                StockCode = code,
                StockName = row.Config.StockName,
                Market = market,
                BenchmarkPriceType = BenchmarkPriceType.PreviousClose,
                BuyThresholdPercent = 2.0m,
                SellThresholdPercent = 2.0m,
                BuyQuantity = 100,
                SellQuantity = 100,
                CooldownSeconds = 60,
                MaxBuyTimesPerDay = 1,
                MaxSellTimesPerDay = 1,
                Status = StockStatus.Stopped,
                TradingHours = "09:30-11:30,13:00-15:00"
            };
            await _stockRepository.SaveConfigAsync(cfg);
            added++;
        }

        MessageBox.Show($"已添加 {added} 只股票到监控列表");
        _logger.Info($"从 AI 选股结果添加 {added} 只股票到监控", "AI");
        await LoadDataAsync();
    }

    /// <summary>
    /// 加载交易日记
    /// </summary>
    private void LoadDiary()
    {
        DiaryEntries.Clear();
        foreach (var entry in _diaryService.GetAll())
            DiaryEntries.Add(entry);
    }

    /// <summary>
    /// 添加日记条目
    /// </summary>
    [RelayCommand]
    private void AddDiary()
    {
        if (string.IsNullOrWhiteSpace(DiaryTitle) && string.IsNullOrWhiteSpace(DiaryContent))
        {
            MessageBox.Show("请输入标题或内容");
            return;
        }

        var entry = _diaryService.Add(DiaryTitle, DiaryContent, DiaryType, DiaryStockCode);
        DiaryEntries.Insert(0, entry);
        DiaryTitle = string.Empty;
        DiaryContent = string.Empty;
        DiaryStockCode = string.Empty;
        _logger.Info($"添加日记：{entry.Title}", "Diary");
    }

    /// <summary>
    /// 加载策略列表
    /// </summary>
    private void LoadStrategies()
    {
        Strategies.Clear();
        foreach (var s in _strategyService.GetAll())
            Strategies.Add(s);
    }

    /// <summary>
    /// 新建策略
    /// </summary>
    [RelayCommand]
    private void NewStrategy()
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("请输入策略名称", "新建策略", "新策略");
        if (string.IsNullOrWhiteSpace(name)) return;

        var formula = Microsoft.VisualBasic.Interaction.InputBox("请输入选股公式（如 CROSS(MA(C,5),MA(C,10))）", "新建策略", "");
        var desc = Microsoft.VisualBasic.Interaction.InputBox("请输入策略描述", "新建策略", "");

        var strategy = new StrategyDefinition
        {
            Name = name,
            Formula = formula,
            Description = desc,
            Author = "用户",
            BuyThresholdPercent = 2.0m,
            SellThresholdPercent = 2.0m,
            BuyQuantity = 100,
            SellQuantity = 100,
            CooldownSeconds = 60
        };
        _strategyService.Save(strategy);
        LoadStrategies();
        _logger.Info($"新建策略：{name}", "Strategy");
    }

    /// <summary>
    /// 导出策略到剪贴板
    /// </summary>
    [RelayCommand]
    private void ExportStrategy()
    {
        if (SelectedStrategy == null)
        {
            MessageBox.Show("请先选择要导出的策略");
            return;
        }
        var json = _strategyService.Export(SelectedStrategy.Id);
        Clipboard.SetText(json);
        MessageBox.Show("策略 JSON 已复制到剪贴板");
    }

    /// <summary>
    /// 从剪贴板导入策略
    /// </summary>
    [RelayCommand]
    private void ImportStrategy()
    {
        var json = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(json))
        {
            MessageBox.Show("剪贴板为空，请先复制策略 JSON");
            return;
        }
        var s = _strategyService.Import(json);
        if (s != null)
        {
            LoadStrategies();
            MessageBox.Show($"已导入策略：{s.Name}");
        }
        else
        {
            MessageBox.Show("导入失败，JSON 格式不正确");
        }
    }

    /// <summary>
    /// 将策略参数应用到当前选中的监控股票
    /// </summary>
    [RelayCommand]
    private async Task ApplyStrategyAsync()
    {
        if (SelectedStrategy == null)
        {
            MessageBox.Show("请先选择策略");
            return;
        }
        if (SelectedStock == null)
        {
            MessageBox.Show("请先在股票监控页选择一只股票");
            return;
        }

        var cfg = SelectedStock.Config;
        cfg.BuyThresholdPercent = SelectedStrategy.BuyThresholdPercent;
        cfg.SellThresholdPercent = SelectedStrategy.SellThresholdPercent;
        cfg.BuyQuantity = SelectedStrategy.BuyQuantity;
        cfg.SellQuantity = SelectedStrategy.SellQuantity;
        cfg.CooldownSeconds = SelectedStrategy.CooldownSeconds;
        await _stockRepository.SaveConfigAsync(cfg);

        SelectedStrategy.UseCount++;
        _strategyService.Save(SelectedStrategy);
        LoadStrategies();
        await LoadDataAsync();
        MessageBox.Show($"已将策略「{SelectedStrategy.Name}」应用到 {cfg.StockCode}");
    }

    /// <summary>
    /// 删除策略
    /// </summary>
    [RelayCommand]
    private void DeleteStrategy()
    {
        if (SelectedStrategy == null)
        {
            MessageBox.Show("请先选择要删除的策略");
            return;
        }
        if (MessageBox.Show($"确定删除策略「{SelectedStrategy.Name}」？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        _strategyService.Delete(SelectedStrategy.Id);
        LoadStrategies();
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
        _settings.WebSocketServerUrl = SettingsWebSocketServerUrl;
        _settings.WebSocketApiKey = SettingsWebSocketApiKey;
        _settings.WebhookUrl = SettingsWebhookUrl;
        _settings.WebhookType = SettingsWebhookType;
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
