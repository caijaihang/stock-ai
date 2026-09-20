using Microsoft.Maui;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.AndroidApp.Services;

/// <summary>
/// Android 系统通知服务
/// 通过 IPlatformApplication 获取原生 Android Context，
/// 使用完全限定名 Android.* 避免与项目命名空间 StockAutoTrader.AndroidApp 冲突。
/// </summary>
public class AndroidNotificationService : INotificationService
{
    private const string ChannelId = "StockAutoTrader";
    private int _nextId = 1000;

    /// <summary>
    /// 是否启用声音
    /// </summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>
    /// 是否启用弹窗
    /// </summary>
    public bool PopupEnabled { get; set; } = true;

    /// <summary>
    /// 是否启用系统通知
    /// </summary>
    public bool SystemToastEnabled { get; set; } = true;

    /// <summary>
    /// 发出系统通知
    /// </summary>
    public void Notify(string title, string message, bool isBuy = false)
    {
        try
        {
            if (!SystemToastEnabled)
            {
                return;
            }

            // 通过 MAUI 平台 API 获取 Android 原生 Application 对象
            // Application.Current 在非 Android 平台（如 WPF）返回 null，直接跳过
            var currentApp = Application.Current;
            if (currentApp is not MauiApplication)
            {
                return;
            }

            // 获取 Android 原生 Context
            var platformApp = Microsoft.Maui.ApplicationModel.PlatformApplication;
            if (!(platformApp?.Context is global::Android.Content.Context context))
            {
                return;
            }

            // 创建通知渠道（Android 8.0 / API 26+）
            if (global::Android.OS.Build.VERSION.SdkInt >= 26)
            {
                var channel = new global::Android.App.NotificationChannel(
                    context,
                    ChannelId,
                    "StockAutoTrader")
                {
                    Importance = global::Android.App.NotificationImportance.High
                };
                var channelManager = (global::Android.App.NotificationManager?)context
                    .GetSystemService(global::Android.Content.Context.NotificationService);
                channelManager?.CreateNotificationChannel(channel);
            }

            var builder = new global::Android.App.Notification.Builder(context, ChannelId)
                .SetSmallIcon(global::Android.Resource.Icon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true)
                .SetColor(isBuy ? 0xFF2E7D32 : 0xFFC62828);

            var manager = (global::Android.App.NotificationManager?)context
                .GetSystemService(global::Android.Content.Context.NotificationService);
            manager?.Notify(_nextId++, builder.Build());
        }
        catch
        {
            // 通知权限未授予或渠道未创建时静默失败
        }
    }
}
