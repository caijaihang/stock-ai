using Android.App;
using Android.Content;
using Android.OS;

namespace StockAutoTrader.AndroidApp;

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
    /// 在运行时请求通知权限（Android 13 / API 33+）
    /// </summary>
    private void RequestNotificationPermission()
    {
        // Android 13 (API 33) 起需要运行时请求通知权限
        if (Build.VERSION.SdkInt < 33)
        {
            return;
        }

        // 权限字符串 "android.permission.POST_NOTIFICATIONS"
        const string postNotificationsPermission = "android.permission.POST_NOTIFICATIONS";

        // 使用 PackageManager.PermissionGranted 常量（值为 0）
        if (CheckSelfPermission(postNotificationsPermission) ==
            Android.Content.PM.PackageManager.PermissionGranted)
        {
            return;
        }

        // RequestPermissions 参数类型为 string[]，避免 string?[] 转换错误
        var permissions = new[] { postNotificationsPermission };
        RequestPermissions(permissions, 1);
    }
}
