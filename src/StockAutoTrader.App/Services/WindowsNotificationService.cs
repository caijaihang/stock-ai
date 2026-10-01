using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StockAutoTrader.Core.Interfaces;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.App.Services;

/// <summary>
/// Windows 通知服务（声音 + 非阻塞弹窗 + 系统 Toast）
/// 弹窗使用独立 Window 而非 MessageBox，避免阻塞 UI 线程
/// </summary>
public class WindowsNotificationService : INotificationService
{
    private readonly Dispatcher _dispatcher;
    private readonly ServiceSettings _settings;
    private readonly WebhookNotifier _webhookNotifier;

    public WindowsNotificationService(ServiceSettings settings, WebhookNotifier webhookNotifier)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _settings = settings;
        _webhookNotifier = webhookNotifier;
    }

    public bool SoundEnabled
    {
        get => _settings.SoundNotification;
        set => _settings.SoundNotification = value;
    }

    public bool PopupEnabled
    {
        get => _settings.PopupNotification;
        set => _settings.PopupNotification = value;
    }

    public bool SystemToastEnabled
    {
        get => _settings.SystemToastNotification;
        set => _settings.SystemToastNotification = value;
    }

    /// <summary>
    /// 发出通知（非阻塞）
    /// </summary>
    public void Notify(string title, string message, bool isBuy = false)
    {
        if (SoundEnabled)
        {
            PlaySound(isBuy ? SystemSounds.Beep : SystemSounds.Exclamation);
        }

        if (PopupEnabled)
        {
            ShowPopup(title, message);
        }

        if (SystemToastEnabled)
        {
            // 系统 Toast 预留（Windows.UI.Notifications）
        }

        // Webhook 推送（飞书/钉钉等），异步不阻塞
        if (!string.IsNullOrWhiteSpace(_settings.WebhookUrl))
        {
            _ = _webhookNotifier.SendAsync(title, message);
        }
    }

    private void PlaySound(SystemSound sound)
    {
        _dispatcher.BeginInvoke(new Action(() => sound.Play()));
    }

    /// <summary>
    /// 非阻塞弹窗：使用独立 Window 并自动关闭
    /// </summary>
    private void ShowPopup(string title, string message)
    {
        _dispatcher.BeginInvoke(new Action(() =>
        {
            var win = new Window
            {
                Title = "StockAutoTrader 通知",
                Width = 320,
                Height = 140,
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                Topmost = true,
                Background = System.Windows.Media.Brushes.White,
                Content = new StackPanel
                {
                    Margin = new Thickness(15),
                    Children =
                    {
                        new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0,0,0,8) },
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
                    }
                }
            };
            // 定位到右下角
            var area = SystemParameters.WorkArea;
            win.Left = area.Right - win.Width - 20;
            win.Top = area.Bottom - win.Height - 20;
            win.Show();
            // 3 秒后自动关闭
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) => { win.Close(); timer.Stop(); };
            timer.Start();
        }));
    }
}
