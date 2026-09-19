using Android.App;
using Android.Content;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Android.Services;

/// <summary>
/// Android 系统通知服务
/// 使用原生 NotificationManager + NotificationChannel，仅依赖稳定的 Android API
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

            // 通过 MAUI Application 获取 Android Context
            var context = (Android.App.Application)Microsoft.Maui.ApplicationModel.Platform.Application.Context;
            var app = Android.App.Application.GetApplicationContext(context);

            // 创建通知渠道（Android 8.0+）
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Oreo)
            {
                var channel = new Android.App.NotificationChannel(app, ChannelId, "StockAutoTrader")
                {
                    Importance = Android.App.NotificationImportance.High
                };
                var channelManager = (Android.App.NotificationManager)app
                    .GetSystemService(Android.Content.Context.NotificationService);
                channelManager.CreateNotificationChannel(channel);
            }

            var builder = new Android.App.Notification.Builder(app, ChannelId)
                .SetSmallIcon(Android.Resource.Icon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true)
                .SetColor(isBuy ? 0xFF2E7D32 : 0xFFC62828);

            var manager = (Android.App.NotificationManager)app
                .GetSystemService(Android.Content.Context.NotificationService);
            manager.Notify(_nextId++, builder.Build());
        }
        catch
        {
            // 通知权限未授予或渠道未创建时静默失败
        }
    }
}
