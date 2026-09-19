using Microsoft.Maui.ApplicationModel;

namespace StockAutoTrader.Android;

/// <summary>
/// MAUI Android 应用类
/// </summary>
public partial class App : MauiApplication
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
