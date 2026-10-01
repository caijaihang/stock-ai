namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 技术指标计算器
/// 支持 MA（移动平均线）、MACD、KDJ 等常用 A 股技术指标
/// </summary>
public static class TechnicalIndicatorCalculator
{
    /// <summary>
    /// 计算简单移动平均线（SMA）
    /// </summary>
    /// <param name="prices">价格序列</param>
    /// <param name="period">周期，如 5、10、20、60</param>
    /// <returns>MA 序列，长度与 prices 相同，不足 period 的位置为 null</returns>
    public static List<decimal?> CalcMA(List<decimal> prices, int period)
    {
        var result = new List<decimal?>(prices.Count);
        if (period <= 0 || prices.Count == 0) return result;

        decimal sum = 0m;
        for (var i = 0; i < prices.Count; i++)
        {
            sum += prices[i];
            if (i >= period) sum -= prices[i - period];

            if (i >= period - 1)
                result.Add(Math.Round(sum / period, 2));
            else
                result.Add(null);
        }
        return result;
    }

    /// <summary>
    /// 计算 MACD 指标
    /// </summary>
    /// <param name="prices">收盘价序列</param>
    /// <param name="fast">快线周期，默认 12</param>
    /// <param name="slow">慢线周期，默认 26</param>
    /// <param name="signal">信号线周期，默认 9</param>
    /// <returns>(DIF, DEA, MACD柱) 三元组列表</returns>
    public static List<(decimal? Dif, decimal? Dea, decimal? Macd)> CalcMACD(
        List<decimal> prices, int fast = 12, int slow = 26, int signal = 9)
    {
        var result = new List<(decimal?, decimal?, decimal?)>(prices.Count);
        if (prices.Count == 0) return result;

        var emaFast = CalcEMA(prices, fast);
        var emaSlow = CalcEMA(prices, slow);
        var difList = new List<decimal?>();
        for (var i = 0; i < prices.Count; i++)
        {
            if (emaFast[i].HasValue && emaSlow[i].HasValue)
                difList.Add(Math.Round(emaFast[i]!.Value - emaSlow[i]!.Value, 3));
            else
                difList.Add(null);
        }

        // DEA = DIF 的 EMA(signal)
        var validDif = difList.Where(d => d.HasValue).Select(d => d!.Value).ToList();
        var deaValid = CalcEMA(validDif, signal);

        var deaList = new List<decimal?>(prices.Count);
        var validIdx = 0;
        foreach (var d in difList)
        {
            if (d.HasValue)
            {
                deaList.Add(validIdx < deaValid.Count ? deaValid[validIdx] : null);
                validIdx++;
            }
            else
            {
                deaList.Add(null);
            }
        }

        // MACD 柱 = 2 * (DIF - DEA)
        for (var i = 0; i < prices.Count; i++)
        {
            if (difList[i].HasValue && deaList[i].HasValue)
            {
                var macd = Math.Round(2 * (difList[i]!.Value - deaList[i]!.Value), 3);
                result.Add((difList[i], deaList[i], macd));
            }
            else
            {
                result.Add((null, null, null));
            }
        }
        return result;
    }

    /// <summary>
    /// 计算 KDJ 指标
    /// </summary>
    /// <param name="highs">最高价序列</param>
    /// <param name="lows">最低价序列</param>
    /// <param name="closes">收盘价序列</param>
    /// <param name="period">RSV 周期，默认 9</param>
    /// <param name="k">K 平滑系数分母，默认 3</param>
    /// <param name="d">D 平滑系数分母，默认 3</param>
    /// <returns>(K, D, J) 三元组列表</returns>
    public static List<(decimal K, decimal D, decimal J)> CalcKDJ(
        List<decimal> highs, List<decimal> lows, List<decimal> closes,
        int period = 9, int k = 3, int d = 3)
    {
        var result = new List<(decimal K, decimal D, decimal J)>(closes.Count);
        if (closes.Count == 0) return result;

        decimal prevK = 50m, prevD = 50m;
        for (var i = 0; i < closes.Count; i++)
        {
            var start = Math.Max(0, i - period + 1);
            var hh = highs.Skip(start).Take(i - start + 1).Max();
            var ll = lows.Skip(start).Take(i - start + 1).Min();

            decimal rsv = hh == ll ? 50m : Math.Round((closes[i] - ll) / (hh - ll) * 100, 2);
            var kVal = Math.Round(((k - 1) * prevK + rsv) / k, 2);
            var dVal = Math.Round(((d - 1) * prevD + kVal) / d, 2);
            var jVal = Math.Round(3 * kVal - 2 * dVal, 2);

            result.Add((kVal, dVal, jVal));
            prevK = kVal;
            prevD = dVal;
        }
        return result;
    }

    /// <summary>
    /// 计算指数移动平均线（EMA）
    /// </summary>
    private static List<decimal?> CalcEMA(List<decimal> prices, int period)
    {
        var result = new List<decimal?>(prices.Count);
        if (prices.Count == 0 || period <= 0) return result;

        decimal ema = prices[0];
        result.Add(ema);
        var multiplier = 2m / (period + 1);
        for (var i = 1; i < prices.Count; i++)
        {
            ema = (prices[i] - ema) * multiplier + ema;
            result.Add(Math.Round(ema, 3));
        }
        return result;
    }
}
