namespace StockAutoTrader.Core;

/// <summary>
/// 便携版路径助手
/// 所有数据（数据库、日志、订单、行情文件）统一存到程序解压目录，
/// 不写入 C 盘 / LocalApplicationData，保证便携化。
/// </summary>
public static class PortablePathHelper
{
    private static string? _overrideAppDirectory;

    /// <summary>
    /// 获取程序所在目录（BaseDirectory，可覆盖便于测试）
    /// </summary>
    public static string AppDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_overrideAppDirectory))
            {
                return _overrideAppDirectory;
            }
            return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    /// <summary>
    /// 设置程序目录（测试用）
    /// </summary>
    public static void SetAppDirectory(string? directory) => _overrideAppDirectory = directory;

    /// <summary>
    /// 获取数据根目录：&lt;app&gt;/data
    /// </summary>
    public static string GetDataDirectory()
    {
        var dir = Path.Combine(AppDirectory, "data");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 获取日志目录：&lt;app&gt;/logs
    /// </summary>
    public static string GetLogDirectory()
    {
        var dir = Path.Combine(AppDirectory, "logs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 获取 SQLite 数据库文件路径：&lt;app&gt;/data/stock_auto_trader.db
    /// </summary>
    public static string GetDbPath() => Path.Combine(GetDataDirectory(), "stock_auto_trader.db");

    /// <summary>
    /// 获取行情文件目录：&lt;app&gt;/data/market
    /// 通达信/同花顺导出的 CSV 或 .day 文件放这里
    /// </summary>
    public static string GetMarketDataDirectory()
    {
        var dir = Path.Combine(GetDataDirectory(), "market");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 获取订单根目录：&lt;app&gt;/data/orders
    /// </summary>
    public static string GetOrdersDirectory()
    {
        var dir = Path.Combine(GetDataDirectory(), "orders");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 获取待处理订单目录：&lt;app&gt;/data/orders/pending
    /// 真实交易执行器把订单写入此目录，交易脚本读取后成交
    /// </summary>
    public static string GetPendingOrdersDirectory()
    {
        var dir = Path.Combine(GetOrdersDirectory(), "pending");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 获取已成交订单目录：&lt;app&gt;/data/orders/filled
    /// 交易脚本把成交回报写入此目录，真实交易执行器读取并更新数据库
    /// </summary>
    public static string GetFilledOrdersDirectory()
    {
        var dir = Path.Combine(GetOrdersDirectory(), "filled");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 获取配置文件路径：&lt;app&gt;/appsettings.json
    /// </summary>
    public static string GetConfigPath() => Path.Combine(AppDirectory, "appsettings.json");

    /// <summary>
    /// 获取 TDX 安装目录下的 lday 数据目录（沪/深）
    /// 用户可配置 TdxInstallDirectory，默认自动探测
    /// </summary>
    public static string GetTdxLdayDirectory(string tdxInstallDirectory, bool isShanghai)
    {
        var market = isShanghai ? "sh" : "sz";
        var dir = Path.Combine(tdxInstallDirectory, "vipdoc", market, "lday");
        return dir;
    }
}
