namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 应用设置
/// </summary>
public class ServiceSettings
{
    /// <summary>
    /// 行情源类型：Simulated / TongDaXin / TongHuaShun
    /// </summary>
    public string MarketDataProvider { get; set; } = "Simulated";

    /// <summary>
    /// 交易执行器类型：Simulated
    /// </summary>
    public string TradeExecutor { get; set; } = "Simulated";

    /// <summary>
    /// 行情刷新频率（毫秒）
    /// </summary>
    public int RefreshIntervalMs { get; set; } = 3000;

    /// <summary>
    /// 初始资金
    /// </summary>
    public decimal InitialCapital { get; set; } = 1000000m;

    /// <summary>
    /// 手续费率
    /// </summary>
    public decimal CommissionRate { get; set; } = 0.0003m;

    /// <summary>
    /// 滑点
    /// </summary>
    public decimal Slippage { get; set; } = 0m;

    /// <summary>
    /// 是否启用 T+1
    /// </summary>
    public bool T1Enabled { get; set; } = true;

    /// <summary>
    /// 通达信导出目录
    /// </summary>
    public string TongDaXinExportDirectory { get; set; } = @"C:\TdxExport";

    /// <summary>
    /// 同花顺导出目录
    /// </summary>
    public string TongHuaShunExportDirectory { get; set; } = @"C:\ThsExport";

    /// <summary>
    /// 日志目录
    /// </summary>
    public string LogDirectory { get; set; } = @"Logs";

    /// <summary>
    /// 数据库路径
    /// </summary>
    public string DatabasePath { get; set; } = @"stock_auto_trader.db";
}
