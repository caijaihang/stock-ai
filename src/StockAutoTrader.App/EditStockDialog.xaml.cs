using System.Windows;
using StockAutoTrader.Core.Entities;

namespace StockAutoTrader.App;

/// <summary>
/// 编辑股票策略对话框
/// </summary>
public partial class EditStockDialog : Window
{
    private readonly StockConfig _config;

    public EditStockDialog(StockConfig config)
    {
        InitializeComponent();
        _config = config;
        DataContext = _config;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
