namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 统一日志接口
/// </summary>
public interface ILoggerService
{
    /// <summary>
    /// 记录调试日志
    /// </summary>
    void Debug(string message, string category = "General", string stockCode = "");

    /// <summary>
    /// 记录信息日志
    /// </summary>
    void Info(string message, string category = "General", string stockCode = "");

    /// <summary>
    /// 记录警告日志
    /// </summary>
    void Warning(string message, string category = "General", string stockCode = "");

    /// <summary>
    /// 记录错误日志
    /// </summary>
    void Error(string message, Exception? exception = null, string category = "General", string stockCode = "");

    /// <summary>
    /// 记录行情日志
    /// </summary>
    void MarketData(string stockCode, string message);

    /// <summary>
    /// 记录策略判断日志
    /// </summary>
    void Strategy(string stockCode, string message);

    /// <summary>
    /// 记录订单日志
    /// </summary>
    void Order(string stockCode, string message);

    /// <summary>
    /// 记录成交日志
    /// </summary>
    void Trade(string stockCode, string message);
}
