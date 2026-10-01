using System.IO;
using StockAutoTrader.Core.Models;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 通达信分钟线文件读取器（.lc1 / .lc5）
/// 文件格式与 .day 相同（每条 32 字节），但日期字段含义不同：
///   .lc1（1分钟线）：日期字段为 YYYYMMDDHHMM 十进制整数（12位）
///   .lc5（5分钟线）：同上
/// 通达信本地分钟线文件路径：vipdoc/&lt;market&gt;/minline/&lt;code&gt;.lc1
/// </summary>
public static class TdxMinuteFileReader
{
    private const int RecordSize = 32;

    /// <summary>
    /// 解析分钟线文件为 K 线记录列表
    /// </summary>
    public static List<KlineRecord> ReadAll(string filePath)
    {
        var records = new List<KlineRecord>();
        if (!File.Exists(filePath))
        {
            return records;
        }

        var bytes = File.ReadAllBytes(filePath);
        var count = bytes.Length / RecordSize;
        for (var i = 0; i < count; i++)
        {
            var offset = i * RecordSize;
            var rawDate = BitConverter.ToUInt32(bytes, offset);
            var open = BitConverter.ToUInt32(bytes, offset + 4) / 100m;
            var high = BitConverter.ToUInt32(bytes, offset + 8) / 100m;
            var low = BitConverter.ToUInt32(bytes, offset + 12) / 100m;
            var close = BitConverter.ToUInt32(bytes, offset + 16) / 100m;
            var amount = BitConverter.ToSingle(bytes, offset + 20);
            var volume = BitConverter.ToUInt32(bytes, offset + 24);

            records.Add(new KlineRecord
            {
                DateTime = ParseMinuteDateTime(rawDate),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Amount = (decimal)amount
            });
        }

        return records;
    }

    /// <summary>
    /// 获取最后 N 条分钟线
    /// </summary>
    public static List<KlineRecord> ReadLastN(string filePath, int n)
    {
        var all = ReadAll(filePath);
        return all.Count > n ? all.GetRange(all.Count - n, n) : all;
    }

    /// <summary>
    /// 解析通达信分钟线日期字段（YYYYMMDDHHMM）
    /// </summary>
    private static DateTime ParseMinuteDateTime(uint rawDate)
    {
        try
        {
            // 通达信分钟线日期为 12 位整数：YYYYMMDDHHMM
            var year = (int)(rawDate / 100000000);
            var month = (int)((rawDate / 1000000) % 100);
            var day = (int)((rawDate / 10000) % 100);
            var hour = (int)((rawDate / 100) % 100);
            var minute = (int)(rawDate % 100);

            if (year < 1990 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31)
            {
                return DateTime.MinValue;
            }
            return new DateTime(year, month, day, hour, minute, 0);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// 解析分钟线文件路径
    /// </summary>
    public static string ResolveMinuteFilePath(string tdxInstallDir, string stockCode, int period = 1)
    {
        var market = stockCode.StartsWith("6") ? "sh" : "sz";
        var ext = period == 5 ? "lc5" : "lc1";
        var fileName = market + stockCode + "." + ext;
        return Path.Combine(tdxInstallDir, "vipdoc", market, "minline", fileName);
    }
}
