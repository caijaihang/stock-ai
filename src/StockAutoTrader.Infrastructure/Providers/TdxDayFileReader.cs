using System;
using System.Collections.Generic;
using System.IO;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 通达信 .day 二进制日数据读取器
///
/// 文件格式（小端序，每条记录 32 字节）：
///   int32   date     - YYYYMMDD
///   int32   open     - 开盘价 × 100
///   int32   high     - 最高价 × 100
///   int32   low      - 最低价 × 100
///   int32   close    - 收盘价 × 100
///   float32 amount   - 成交额（元）
///   int32   volume   - 成交量（手）
///   int32   reserved - 保留
///
/// 文件位置：
///   沪市：<TDX_INSTALL>/vipdoc/sh/lday/sh600000.day
///   深市：<TDX_INSTALL>/vipdoc/sz/lday/sz000001.day
/// </summary>
public static class TdxDayFileReader
{
    /// <summary>
    /// 每条记录大小（字节）
    /// </summary>
    public const int RecordSize = 32;

    /// <summary>
    /// 从 .day 文件读取最后一天的收盘价
    /// </summary>
    public static decimal? ReadLastClose(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < RecordSize)
            {
                return null;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(fileInfo.Length - RecordSize, SeekOrigin.Begin);
            var buffer = new byte[RecordSize];
            var bytesRead = fs.Read(buffer, 0, RecordSize);
            if (bytesRead < RecordSize)
            {
                return null;
            }

            // close 位于第 16 字节（date/open/high/low 各 4 字节之后）
            var closeInt = BitConverter.ToInt32(buffer, 16);
            return closeInt / 100m;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读取指定日期的记录
    /// </summary>
    public static TdxDayRecord? ReadByDate(string filePath, int targetDate)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var recordCount = (int)(fileInfo.Length / RecordSize);
            if (recordCount == 0)
            {
                return null;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[RecordSize];

            // 从后往前线性搜索目标日期（日数据按日期升序排列）
            for (int i = recordCount - 1; i >= 0; i--)
            {
                fs.Seek(i * (long)RecordSize, SeekOrigin.Begin);
                fs.Read(buffer, 0, RecordSize);

                var date = BitConverter.ToInt32(buffer, 0);
                if (date == targetDate)
                {
                    return ParseRecord(buffer);
                }
                if (date < targetDate)
                {
                    break;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读取最后 N 天的数据
    /// </summary>
    public static List<TdxDayRecord> ReadLastNDays(string filePath, int count)
    {
        var result = new List<TdxDayRecord>();
        if (!File.Exists(filePath) || count <= 0)
        {
            return result;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var totalRecords = (int)(fileInfo.Length / RecordSize);
            var startRecord = Math.Max(0, totalRecords - count);

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[RecordSize];

            for (int i = startRecord; i < totalRecords; i++)
            {
                fs.Seek(i * (long)RecordSize, SeekOrigin.Begin);
                var bytesRead = fs.Read(buffer, 0, RecordSize);
                if (bytesRead < RecordSize)
                {
                    break;
                }
                var record = ParseRecord(buffer);
                if (record != null)
                {
                    result.Add(record);
                }
            }
        }
        catch
        {
            // 读取失败返回已读部分
        }
        return result;
    }

    /// <summary>
    /// 将 6 位股票代码转换为 TDX 文件路径（沪/深自动判断）
    /// 沪市：600xxx / 601xxx / 603xxx / 688xxx → sh600000.day
    /// 深市：000xxx / 001xxx / 002xxx / 003xxx / 300xxx / 301xxx → sz000001.day
    /// </summary>
    public static string ResolveDayFilePath(string tdxInstallDirectory, string stockCode)
    {
        // 判断市场
        bool isShanghai = stockCode.StartsWith("6") || stockCode.StartsWith("9");
        var market = isShanghai ? "sh" : "sz";
        var filename = $"{market}{stockCode}.day";
        return Path.Combine(tdxInstallDirectory, "vipdoc", market, "lday", filename);
    }

    /// <summary>
    /// 解析单条 32 字节记录
    /// </summary>
    private static TdxDayRecord? ParseRecord(byte[] buffer)
    {
        var date = BitConverter.ToInt32(buffer, 0);
        if (date < 19900101 || date > 20991231)
        {
            return null;
        }

        return new TdxDayRecord
        {
            Date = date,
            Open = BitConverter.ToInt32(buffer, 4) / 100m,
            High = BitConverter.ToInt32(buffer, 8) / 100m,
            Low = BitConverter.ToInt32(buffer, 12) / 100m,
            Close = BitConverter.ToInt32(buffer, 16) / 100m,
            Amount = BitConverter.ToSingle(buffer, 20),
            Volume = BitConverter.ToInt32(buffer, 24)
        };
    }
}

/// <summary>
/// 通达信日数据记录
/// </summary>
public class TdxDayRecord
{
    /// <summary>
    /// 日期（YYYYMMDD）
    /// </summary>
    public int Date { get; set; }

    /// <summary>
    /// 开盘价
    /// </summary>
    public decimal Open { get; set; }

    /// <summary>
    /// 最高价
    /// </summary>
    public decimal High { get; set; }

    /// <summary>
    /// 最低价
    /// </summary>
    public decimal Low { get; set; }

    /// <summary>
    /// 收盘价
    /// </summary>
    public decimal Close { get; set; }

    /// <summary>
    /// 成交额（元）
    /// </summary>
    public float Amount { get; set; }

    /// <summary>
    /// 成交量（手）
    /// </summary>
    public int Volume { get; set; }
}
