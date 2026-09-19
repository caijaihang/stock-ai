using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;

namespace StockAutoTrader.Android;

/// <summary>
/// Android 主 Activity（.NET MAUI 官方模板标准写法）
/// </summary>
[Activity(
    Theme = "@style/MainTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// 重写 OnCreate 请求通知权限（Android 13+）
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
        if (Build.VERSION.SdkInt >= AndroidVersionCodes.Tiramisu)
        {
            var permission = Manifest.Permission.PostNotifications;
            if (CheckSelfPermission(permission) == PackageManager.PermissionGranted)
            {
                return;
            }
            RequestPermissions(new[] { permission }, 0);
        }
    }
}
