using Android.Content.Res;
using Android.OS;

namespace StockAutoTrader.Android;

/// <summary>
/// Android 主 Activity（MAUI 标准写法）
/// </summary>
[Android.App.Activity(
    Theme = "@style/MainTheme",
    ConfigurationChanges = Android.Content.Res.ConfigChanges.ScreenSize |
                          Android.Content.Res.ConfigChanges.Orientation |
                          Android.Content.Res.ConfigChanges.UiMode |
                          Android.Content.Res.ConfigChanges.ScreenLayout |
                          Android.Content.Res.ConfigChanges.SmallestScreenSize |
                          Android.Content.Res.ConfigChanges.Density)]
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
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
        {
            var permission = Android.Manifest.Permission.PostNotifications;
            if (CheckSelfPermission(permission) != Android.Content.PM.PackageManager.PermissionGranted)
            {
                RequestPermissions(new[] { permission }, 0);
            }
        }
    }
}
