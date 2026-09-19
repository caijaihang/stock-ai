using Android.Content;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Android.Services;

/// <summary>
/// Android 系统通知服务
/// 通过 MAUI 平台 API（IPlatformApplication）访问原生 Context，
/// 避免直接引用 Android.* 命名空间导致与我们命名空间重名冲突。
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

            // 获取 MAUI 平台服务对象（跨平台安全）
            var platformApp = Microsoft.Maui.ApplicationModel.PlatformApplication.Current
                ?? throw new InvalidOperationException("PlatformApplication 未初始化");
            var androidApp = platformApp.Application as Android.App.Application;
            if (androidApp == null)
            {
                return; // 非 Android 平台直接跳过
            }

            var context = androidApp.BaseContext;

            // 创建通知渠道（Android 8.0+）
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Oreo)
            {
                var channel = new Android.App.NotificationChannel(context, ChannelId, "StockAutoTrader")
                {
                    Importance = Android.App.NotificationImportance.High
                };
                var channelManager = (Android.App.NotificationManager)context
                    .GetSystemService(Content.NotificationService);
                channelManager.CreateNotificationChannel(channel);
            }

            var builder = new Android.App.Notification.Builder(context, ChannelId)
                .SetSmallIcon(Android.Resource.Icon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true)
                .SetColor(isBuy ? 0xFF2E7D32 : 0xFFC62828);

            var manager = (Android.App.NotificationManager)context
                .GetSystemService(Content.NotificationService);
            manager.Notify(_nextId++, builder.Build());
        }
        catch
        {
            // 通知权限未授予或渠道未创建时静默失败
        }
    }
}
