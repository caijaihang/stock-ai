namespace StockAutoTrader.Core.Extensions;

/// <summary>
/// DateTime 扩展方法
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// 判断当前时间是否在指定交易时段内
    /// </summary>
    public static bool IsInTradingHours(this DateTime time, string tradingHours)
    {
        if (string.IsNullOrWhiteSpace(tradingHours))
        {
            return true;
        }

        var segments = tradingHours.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var current = time.TimeOfDay;

        foreach (var segment in segments)
        {
            var parts = segment.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (TimeSpan.TryParse(parts[0].Trim(), out var start) &&
                TimeSpan.TryParse(parts[1].Trim(), out var end))
            {
                if (current >= start && current <= end)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
