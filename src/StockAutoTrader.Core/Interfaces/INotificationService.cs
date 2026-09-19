namespace StockAutoTrader.Core.Interfaces;

/// <summary>
/// 通知服务接口（声音、弹窗、系统通知）
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 是否启用声音
    /// </summary>
    bool SoundEnabled { get; set; }

    /// <summary>
    /// 是否启用弹窗
    /// </summary>
    bool PopupEnabled { get; set; }

    /// <summary>
    /// 是否启用系统通知
    /// </summary>
    bool SystemToastEnabled { get; set; }

    /// <summary>
    /// 发出通知
    /// </summary>
    void Notify(string title, string message, bool isBuy = false);
}
