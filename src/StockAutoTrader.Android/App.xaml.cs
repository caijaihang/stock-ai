namespace StockAutoTrader.Android;

/// <summary>
/// MAUI Android 应用类
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
