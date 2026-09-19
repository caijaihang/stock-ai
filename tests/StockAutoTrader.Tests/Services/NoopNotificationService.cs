using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Tests.Services;

/// <summary>
/// 空通知服务（测试用）
/// </summary>
public class NoopNotificationService : INotificationService
{
    public bool SoundEnabled { get; set; }
    public bool PopupEnabled { get; set; }
    public bool SystemToastEnabled { get; set; }

    public void Notify(string title, string message, bool isBuy = false)
    {
        // no-op
    }
}
