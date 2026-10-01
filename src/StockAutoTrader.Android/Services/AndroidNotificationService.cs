using Android.App;
using Android.Content;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.AndroidApp.Services;

/// <summary>
/// Android 系统通知服务
/// 使用 Android 原生 API 发送通知，全限定名避免命名空间冲突。
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

            // 获取 Android 应用级 Context（全局单例，不受 Activity 生命周期影响）
            var context = global::Android.App.Application.Context;
            if (context is null)
            {
                return;
            }

            // 通知渠道与带 channelId 的 Builder 仅在 Android 8.0 (API 26) 及以上可用
            if (!OperatingSystem.IsAndroidVersionAtLeast(8))
            {
                return;
            }

            // 创建通知渠道（Android 8.0 / API 26+）
            {
                var channel = new global::Android.App.NotificationChannel(
                    ChannelId,
                    new global::Java.Lang.String("StockAutoTrader"),
                    global::Android.App.NotificationImportance.High);
                var channelManager = (global::Android.App.NotificationManager?)context
                    .GetSystemService(global::Android.Content.Context.NotificationService);
                channelManager?.CreateNotificationChannel(channel);
            }

            var builder = new global::Android.App.Notification.Builder(context, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetAutoCancel(true)
                .SetColor((int)(isBuy ? 0xFF2E7D32 : 0xFFC62828));

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
