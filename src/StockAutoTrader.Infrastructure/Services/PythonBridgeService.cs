using System.Diagnostics;
using System.Text;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// Python 桥接服务
/// 用于调用外部 Python 交易工具（如 tdxtrader、EasyTHS、eltdx、AxData、TdxQuant 等）
/// 通过进程启动 Python 脚本并解析输出
/// </summary>
public class PythonBridgeService
{
    private readonly string _pythonPath;
    private readonly string _scriptsDir;

    public PythonBridgeService()
    {
        _pythonPath = "python"; // 默认使用系统 python
        _scriptsDir = Path.Combine(StockAutoTrader.Core.PortablePathHelper.GetDataDirectory(), "python_scripts");
        if (!Directory.Exists(_scriptsDir)) Directory.CreateDirectory(_scriptsDir);
    }

    /// <summary>
    /// 执行 Python 脚本并返回输出
    /// </summary>
    /// <param name="scriptPath">脚本路径（绝对路径或相对于 scripts 目录）</param>
    /// <param name="args">命令行参数</param>
    /// <returns>(标准输出, 标准错误, 退出码)</returns>
    public async Task<(string stdout, string stderr, int exitCode)> RunScriptAsync(string scriptPath, params string[] args)
    {
        var fullPath = Path.IsPathRooted(scriptPath) ? scriptPath : Path.Combine(_scriptsDir, scriptPath);
        if (!File.Exists(fullPath))
        {
            return (string.Empty, $"脚本不存在：{fullPath}", -1);
        }

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{fullPath}\" {string.Join(" ", args.Select(a => $"\"{a}\""))}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (stdoutBuilder.ToString().Trim(), stderrBuilder.ToString().Trim(), process.ExitCode);
    }

    /// <summary>
    /// 执行内联 Python 代码
    /// </summary>
    /// <param name="code">Python 代码</param>
    /// <param name="args">命令行参数</param>
    public async Task<(string stdout, string stderr, int exitCode)> RunInlineAsync(string code, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"-c \"{code.Replace("\"", "\\\"")}\" {string.Join(" ", args.Select(a => $"\"{a}\""))}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (stdoutBuilder.ToString().Trim(), stderrBuilder.ToString().Trim(), process.ExitCode);
    }

    /// <summary>
    /// 检查 Python 环境是否可用
    /// </summary>
    public async Task<bool> IsPythonAvailableAsync()
    {
        try
        {
            var (_, _, exitCode) = await RunInlineAsync("import sys; print(sys.version)");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取 Python 版本
    /// </summary>
    public async Task<string> GetPythonVersionAsync()
    {
        var (stdout, _, _) = await RunInlineAsync("import sys; print(sys.version.split()[0])");
        return stdout;
    }

    /// <summary>
    /// 保存 Python 脚本到 scripts 目录
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="content">脚本内容</param>
    public string SaveScript(string fileName, string content)
    {
        var path = Path.Combine(_scriptsDir, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// 获取 scripts 目录下所有脚本
    /// </summary>
    public List<string> ListScripts()
    {
        if (!Directory.Exists(_scriptsDir)) return new List<string>();
        return Directory.GetFiles(_scriptsDir, "*.py").Select(Path.GetFileName).ToList()!;
    }
}
