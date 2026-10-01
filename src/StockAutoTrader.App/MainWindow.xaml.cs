using System.Windows;
using System.Windows.Controls;

namespace StockAutoTrader.App;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 刷新间隔下拉框变更时通知 ViewModel
    /// </summary>
    private void RefreshInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm && vm.ChangeRefreshIntervalCommand.CanExecute(null))
        {
            vm.ChangeRefreshIntervalCommand.Execute(null);
        }
    }
}
