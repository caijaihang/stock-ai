namespace StockAutoTrader.Core.Exceptions;

/// <summary>
/// 交易业务异常
/// </summary>
public class TradingException : Exception
{
    public TradingException(string message) : base(message)
    {
    }

    public TradingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
