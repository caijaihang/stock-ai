using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace StockAutoTrader.Android;

/// <summary>
/// Android 主 Activity
/// </summary>
[Activity(
    Theme = "@style/MainTheme",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                       ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppActivity
{
    /// <summary>
    /// 请求通知权限（Android 13+）
    /// </summary>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestNotificationPermission();
    }

    /// <summary>
    /// 在运行时请求通知权限
    /// </summary>
    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            var permission = Manifest.PermissionName.PostNotifications;
            if (CheckSelfPermission(permission) != Android.Content.PM.PackageManager.PermissionGranted)
            {
                RequestPermissions(new[] { permission }, 0);
            }
        }
    }
}
