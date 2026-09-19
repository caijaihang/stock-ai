using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Android.Services;

/// <summary>
/// 无操作日志服务（Android 端占位实现，后续可接 Serilog Android 落盘）
/// </summary>
public class NoopLoggerService : ILoggerService
{
    /// <inheritdoc />
    public void Debug(string message, string category = "General", string stockCode = "")
    {
    }

    /// <inheritdoc />
    public void Info(string message, string category = "General", string stockCode = "")
    {
    }

    /// <inheritdoc />
    public void Warning(string message, string category = "General", string stockCode = "")
    {
    }

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null, string category = "General", string stockCode = "")
    {
    }

    /// <inheritdoc />
    public void MarketData(string stockCode, string message)
    {
    }

    /// <inheritdoc />
    public void Strategy(string stockCode, string message)
    {
    }

    /// <inheritdoc />
    public void Order(string stockCode, string message)
    {
    }

    /// <inheritdoc />
    public void Trade(string stockCode, string message)
    {
    }
}
