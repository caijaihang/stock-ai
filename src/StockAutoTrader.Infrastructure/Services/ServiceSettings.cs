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
    /// 通达信安装目录（用于读取 vipdoc 下的 .day 日数据文件）
    /// </summary>
    public string TdxInstallDirectory { get; set; } = @"";

    /// <summary>
    /// 通达信行情文件目录（导出 CSV 放这里；留空则自动使用 data/market）
    /// </summary>
    public string TongDaXinExportDirectory { get; set; } = "";

    /// <summary>
    /// 同花顺行情文件目录（导出 CSV 放这里；留空则自动使用 data/market）
    /// </summary>
    public string TongHuaShunExportDirectory { get; set; } = "";

    /// <summary>
    /// 日志目录（留空则自动使用程序目录下的 logs/）
    /// </summary>
    public string LogDirectory { get; set; } = "";

    /// <summary>
    /// 数据库路径（留空则自动使用程序目录下的 data/stock_auto_trader.db，便携版）
    /// </summary>
    public string DatabasePath { get; set; } = "";

    /// <summary>
    /// 待处理订单目录（留空则自动使用程序目录下的 data/orders/pending/）
    /// </summary>
    public string PendingOrdersDirectory { get; set; } = "";

    /// <summary>
    /// 已成交回报目录（留空则自动使用程序目录下的 data/orders/filled/）
    /// </summary>
    public string FilledOrdersDirectory { get; set; } = "";

    /// <summary>
    /// 解析数据库路径：留空则使用便携目录 data/stock_auto_trader.db
    /// </summary>
    public string ResolveDatabasePath()
    {
        return string.IsNullOrWhiteSpace(DatabasePath)
            ? StockAutoTrader.Core.PortablePathHelper.GetDbPath()
            : DatabasePath;
    }

    /// <summary>
    /// 解析日志目录：留空则使用便携目录 logs/
    /// </summary>
    public string ResolveLogDirectory()
    {
        return string.IsNullOrWhiteSpace(LogDirectory)
            ? StockAutoTrader.Core.PortablePathHelper.GetLogDirectory()
            : LogDirectory;
    }

    /// <summary>
    /// 解析通达信行情文件目录：留空则使用便携目录 data/market
    /// </summary>
    public string ResolveTongDaXinExportDirectory()
    {
        return string.IsNullOrWhiteSpace(TongDaXinExportDirectory)
            ? StockAutoTrader.Core.PortablePathHelper.GetMarketDataDirectory()
            : TongDaXinExportDirectory;
    }

    /// <summary>
    /// 解析同花顺行情文件目录：留空则使用便携目录 data/market
    /// </summary>
    public string ResolveTongHuaShunExportDirectory()
    {
        return string.IsNullOrWhiteSpace(TongHuaShunExportDirectory)
            ? StockAutoTrader.Core.PortablePathHelper.GetMarketDataDirectory()
            : TongHuaShunExportDirectory;
    }

    /// <summary>
    /// 解析待处理订单目录：留空则使用便携目录 data/orders/pending
    /// </summary>
    public string ResolvePendingOrdersDirectory()
    {
        return string.IsNullOrWhiteSpace(PendingOrdersDirectory)
            ? StockAutoTrader.Core.PortablePathHelper.GetPendingOrdersDirectory()
            : PendingOrdersDirectory;
    }

    /// <summary>
    /// 解析已成交回报目录：留空则使用便携目录 data/orders/filled
    /// </summary>
    public string ResolveFilledOrdersDirectory()
    {
        return string.IsNullOrWhiteSpace(FilledOrdersDirectory)
            ? StockAutoTrader.Core.PortablePathHelper.GetFilledOrdersDirectory()
            : FilledOrdersDirectory;
    }
}
