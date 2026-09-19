using System.Media;
using System.Windows;
using System.Windows.Threading;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.App.Services;

/// <summary>
/// Windows 通知服务实现（声音 + 弹窗 + 系统 Toast）
/// </summary>
public class WindowsNotificationService : INotificationService
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// 构造函数
    /// </summary>
    public WindowsNotificationService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public bool SoundEnabled { get; set; } = true;
    public bool PopupEnabled { get; set; } = true;
    public bool SystemToastEnabled { get; set; } = false;

    /// <summary>
    /// 发出通知
    /// </summary>
    public void Notify(string title, string message, bool isBuy = false)
    {
        // 1. 声音
        if (SoundEnabled)
        {
            PlaySound(isBuy ? SystemSounds.Beep : SystemSounds.Exclamation);
        }

        // 2. 弹窗（可选）
        if (PopupEnabled)
        {
            ShowPopup(title, message);
        }

        // 3. 系统 Toast（预留）
        if (SystemToastEnabled)
        {
            // 实际可用 Windows.UI.Notifications 或 TaskbarToast，此处预留
        }
    }

    /// <summary>
    /// 播放声音
    /// </summary>
    private void PlaySound(SystemSound sound)
    {
        _dispatcher.Invoke(() => sound.Play());
    }

    /// <summary>
    /// 显示弹窗
    /// </summary>
    private void ShowPopup(string title, string message)
    {
        _dispatcher.Invoke(() =>
        {
            MessageBox.Show($"{title}\n{message}", "StockAutoTrader 通知", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}
