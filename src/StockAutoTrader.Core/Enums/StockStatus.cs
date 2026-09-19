namespace StockAutoTrader.Core.Enums;

/// <summary>
/// 股票监控状态
/// </summary>
public enum StockStatus
{
    /// <summary>已停止</summary>
    Stopped = 0,

    /// <summary>运行中</summary>
    Running = 1,

    /// <summary>暂停</summary>
    Paused = 2,

    /// <summary>错误</summary>
    Error = 3
}
