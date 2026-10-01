using System.IO;
using System.Text;

namespace StockAutoTrader.Infrastructure.Providers;

/// <summary>
/// 通达信自选股板块文件读取器
/// 自选股文件位于：&lt;通达信安装目录&gt;/T0002/blocknew/
/// 文件格式：
///   - 第一行：板块名称（如"自选股"）
///   - 后续行：股票代码（7位，如 sh600000）或仅代码（6位）
/// 也支持 .blk 二进制格式：每个代码为 7 字节 ASCII（如 sh600000），或带 1 字节长度前缀
/// </summary>
public static class TdxBlockNewReader
{
    /// <summary>
    /// 读取自选股列表（返回 6 位股票代码列表）
    /// </summary>
    /// <param name="tdxInstallDir">通达信安装目录</param>
    /// <param name="blockFileName">板块文件名，默认 ZXG.blk（自选股）</param>
    public static List<string> ReadBlock(string tdxInstallDir, string blockFileName = "ZXG.blk")
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(tdxInstallDir))
        {
            return result;
        }

        var blockDir = Path.Combine(tdxInstallDir, "T0002", "blocknew");
        if (!Directory.Exists(blockDir))
        {
            return result;
        }

        // 优先找 ZXG.blk，找不到则遍历所有 .blk 文件
        var blockFile = Path.Combine(blockDir, blockFileName);
        if (!File.Exists(blockFile))
        {
            var files = Directory.GetFiles(blockDir, "*.blk");
            if (files.Length == 0) return result;
            blockFile = files[0];
        }

        try
        {
            var bytes = File.ReadAllBytes(blockFile);
            // 判断是文本还是二进制
            var isText = bytes.Length > 0 && (bytes[0] < 0x20 || bytes[0] > 0x7e || IsPrintable(bytes));
            if (isText)
            {
                ParseTextFormat(bytes, result);
            }
            else
            {
                ParseBinaryFormat(bytes, result);
            }
        }
        catch
        {
            // 读取失败时返回空列表
        }

        return result;
    }

    /// <summary>
    /// 列出所有板块名称
    /// </summary>
    public static List<string> ListBlocks(string tdxInstallDir)
    {
        var blocks = new List<string>();
        if (string.IsNullOrWhiteSpace(tdxInstallDir)) return blocks;

        var blockDir = Path.Combine(tdxInstallDir, "T0002", "blocknew");
        if (!Directory.Exists(blockDir)) return blocks;

        foreach (var file in Directory.GetFiles(blockDir, "*.blk"))
        {
            blocks.Add(Path.GetFileNameWithoutExtension(file));
        }
        return blocks;
    }

    private static bool IsPrintable(byte[] bytes)
    {
        foreach (var b in bytes)
        {
            if (b == 0x0d || b == 0x0a) continue;
            if (b < 0x20 || b > 0x7e) return false;
        }
        return true;
    }

    private static void ParseTextFormat(byte[] bytes, List<string> result)
    {
        var text = Encoding.Default.GetString(bytes);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        // 第一行通常是板块名称，跳过
        foreach (var line in lines.Skip(1))
        {
            var code = ExtractStockCode(line.Trim());
            if (code != null) result.Add(code);
        }
    }

    private static void ParseBinaryFormat(byte[] bytes, List<string> result)
    {
        // 二进制格式：每个条目为 7 字节 ASCII 代码（如 sh600000）
        // 或 1 字节长度 + N 字节代码
        var i = 0;
        while (i < bytes.Length)
        {
            // 尝试 7 字节固定格式
            if (i + 7 <= bytes.Length)
            {
                var segment = Encoding.ASCII.GetString(bytes, i, 7);
                var code = ExtractStockCode(segment);
                if (code != null)
                {
                    result.Add(code);
                    i += 7;
                    continue;
                }
            }
            i++;
        }
    }

    /// <summary>
    /// 从字符串中提取 6 位股票代码
    /// </summary>
    private static string? ExtractStockCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // 去除 sh/sz 前缀
        var cleaned = input.Replace("sh", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("sz", "", StringComparison.OrdinalIgnoreCase)
                           .Trim();

        // 提取 6 位数字
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());
        if (digits.Length >= 6)
        {
            return digits.Substring(0, 6);
        }
        return null;
    }
}
