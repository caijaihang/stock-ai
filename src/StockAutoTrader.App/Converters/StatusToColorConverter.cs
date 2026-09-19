using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StockAutoTrader.Core.Enums;

namespace StockAutoTrader.App.Converters;

/// <summary>
/// 股票状态转颜色
/// </summary>
[ValueConversion(typeof(StockStatus), typeof(Brush))]
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StockStatus status)
        {
            return status switch
            {
                StockStatus.Running => Brushes.Green,
                StockStatus.Stopped => Brushes.Gray,
                StockStatus.Paused => Brushes.Orange,
                StockStatus.Error => Brushes.Red,
                _ => Brushes.Black
            };
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
