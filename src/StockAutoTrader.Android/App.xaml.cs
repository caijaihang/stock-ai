using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace StockAutoTrader.AndroidApp;

/// <summary>
/// MAUI Android 应用类（Android 平台 MainApplication）
/// </summary>
public partial class App : MauiApplication
{
    /// <summary>
    /// JNI 构造函数
    /// </summary>
    public App(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
    {
    }

    /// <summary>
    /// 创建 MAUI 应用
    /// </summary>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
