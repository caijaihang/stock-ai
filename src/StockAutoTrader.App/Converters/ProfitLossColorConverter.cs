using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StockAutoTrader.App.Converters;

/// <summary>
/// 盈亏数值转颜色
/// </summary>
[ValueConversion(typeof(decimal), typeof(Brush))]
public class ProfitLossColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            return d switch
            {
                > 0 => Brushes.Green,
                < 0 => Brushes.Red,
                _ => Brushes.Gray
            };
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
