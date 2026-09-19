using Serilog;
using Serilog.Core;
using StockAutoTrader.Core.Entities;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Data;

namespace StockAutoTrader.Infrastructure.Logging;

/// <summary>
/// Serilog 统一日志服务实现
/// </summary>
public class SerilogLoggerService : ILoggerService, IDisposable
{
    private readonly Serilog.Core.Logger _logger;
    private readonly TradingDbContext? _context;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SerilogLoggerService(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Category}] [{StockCode}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Category}] [{StockCode}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// 构造函数（带数据库持久化）
    /// </summary>
    public SerilogLoggerService(string logDirectory, TradingDbContext context) : this(logDirectory)
    {
        _context = context;
    }

    /// <summary>
    /// 记录调试日志
    /// </summary>
    public void Debug(string message, string category = "General", string stockCode = "")
    {
        _logger.ForContext("Category", category).ForContext("StockCode", stockCode).Debug(message);
    }

    /// <summary>
    /// 记录信息日志
    /// </summary>
    public void Info(string message, string category = "General", string stockCode = "")
    {
        _logger.ForContext("Category", category).ForContext("StockCode", stockCode).Information(message);
        PersistLog("Info", category, stockCode, message);
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    public void Warning(string message, string category = "General", string stockCode = "")
    {
        _logger.ForContext("Category", category).ForContext("StockCode", stockCode).Warning(message);
        PersistLog("Warning", category, stockCode, message);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    public void Error(string message, Exception? exception = null, string category = "General", string stockCode = "")
    {
        _logger.ForContext("Category", category).ForContext("StockCode", stockCode).Error(exception, message);
        PersistLog("Error", category, stockCode, message, exception?.ToString());
    }

    /// <summary>
    /// 记录行情日志
    /// </summary>
    public void MarketData(string stockCode, string message)
    {
        Info(message, "MarketData", stockCode);
    }

    /// <summary>
    /// 记录策略判断日志
    /// </summary>
    public void Strategy(string stockCode, string message)
    {
        Info(message, "Strategy", stockCode);
    }

    /// <summary>
    /// 记录订单日志
    /// </summary>
    public void Order(string stockCode, string message)
    {
        Info(message, "Order", stockCode);
    }

    /// <summary>
    /// 记录成交日志
    /// </summary>
    public void Trade(string stockCode, string message)
    {
        Info(message, "Trade", stockCode);
    }

    /// <summary>
    /// 持久化日志到数据库
    /// </summary>
    private void PersistLog(string level, string category, string stockCode, string message, string? exception = null)
    {
        if (_context == null)
        {
            return;
        }

        try
        {
            _context.TradingLogs.Add(new TradingLog
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                StockCode = stockCode,
                Message = message,
                Exception = exception
            });
            _context.SaveChanges();
        }
        catch
        {
            // 持久化失败时不影响主流程
        }
    }

    public void Dispose()
    {
        _logger.Dispose();
        GC.SuppressFinalize(this);
    }
}
