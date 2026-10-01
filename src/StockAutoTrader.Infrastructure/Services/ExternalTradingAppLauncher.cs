using System.Diagnostics;
using StockAutoTrader.Infrastructure.Services;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 外部行情软件启动器
/// 内置启动通达信/同花顺，支持检查运行状态和关闭
/// </summary>
public class ExternalTradingAppLauncher
{
    private readonly ServiceSettings _settings;

    public ExternalTradingAppLauncher(ServiceSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 启动通达信
    /// </summary>
    public bool LaunchTongDaXin()
    {
        return LaunchProcess(_settings.TongDaXinExePath, "tdxw.exe");
    }

    /// <summary>
    /// 启动同花顺
    /// </summary>
    public bool LaunchTongHuaShun()
    {
        return LaunchProcess(_settings.TongHuaShunExePath, "xiadan.exe");
    }

    /// <summary>
    /// 检查通达信是否运行
    /// </summary>
    public bool IsTongDaXinRunning() => IsProcessRunning("tdxw");

    /// <summary>
    /// 检查同花顺是否运行
    /// </summary>
    public bool IsTongHuaShunRunning() => IsProcessRunning("xiadan");

    /// <summary>
    /// 关闭通达信
    /// </summary>
    public void CloseTongDaXin() => CloseProcess("tdxw");

    /// <summary>
    /// 关闭同花顺
    /// </summary>
    public void CloseTongHuaShun() => CloseProcess("xiadan");

    /// <summary>
    /// 启动进程
    /// </summary>
    private static bool LaunchProcess(string exePath, string defaultExeName)
    {
        try
        {
            var path = exePath;
            // 如果配置的是目录，拼上默认 exe 名
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                path = Path.Combine(path, defaultExeName);
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                // 尝试在常见安装目录查找
                path = FindInCommonDirectories(defaultExeName);
                if (path == null) return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path)
            };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 在常见安装目录查找程序
    /// </summary>
    private static string? FindInCommonDirectories(string exeName)
    {
        var searchRoots = new[]
        {
            @"C:\new_tdx",
            @"C:\通达信",
            @"C:\同花顺",
            @"D:\new_tdx",
            @"D:\同花顺",
            @"C:\Program Files\",
            @"C:\Program Files (x86)\"
        };

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var found = Directory.GetFiles(root, exeName, SearchOption.AllDirectories).FirstOrDefault();
                if (found != null) return found;
            }
            catch
            {
                // 权限不足时跳过
            }
        }
        return null;
    }

    /// <summary>
    /// 检查进程是否运行
    /// </summary>
    private static bool IsProcessRunning(string processName)
    {
        return Process.GetProcessesByName(processName).Length > 0;
    }

    /// <summary>
    /// 关闭进程
    /// </summary>
    private static void CloseProcess(string processName)
    {
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try
            {
                p.CloseMainWindow();
                if (!p.WaitForExit(3000))
                {
                    p.Kill();
                }
            }
            catch { /* ignore */ }
        }
    }
}
