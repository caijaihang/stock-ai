using System.Diagnostics;
using StockAutoTrader.Core.Interfaces;

namespace StockAutoTrader.Infrastructure.Services;

/// <summary>
/// 内存监控服务（适配 Win7 4GB 总内存 / 3GB 可用）
/// 定期检测进程内存占用，超过阈值时主动触发 GC 并通知用户
/// </summary>
public class MemoryMonitorService : IDisposable
{
    private readonly INotificationService _notificationService;
    private readonly ILoggerService _logger;
    private readonly Timer _timer;
    private readonly long _alertThresholdBytes;
    private readonly long _criticalThresholdBytes;
    private DateTime _lastAlertTime = DateTime.MinValue;

    /// <summary>当前进程内存（MB）</summary>
    public long CurrentMemoryMb { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="notificationService">通知服务</param>
    /// <param name="logger">日志服务</param>
    /// <param name="alertThresholdMb">告警阈值（MB），默认 800MB</param>
    /// <param name="criticalThresholdMb">严重阈值（MB），默认 1500MB，超过则强制 GC</param>
    /// <param name="checkIntervalMs">检测间隔（毫秒），默认 30 秒</param>
    public MemoryMonitorService(
        INotificationService notificationService,
        ILoggerService logger,
        long alertThresholdMb = 800,
        long criticalThresholdMb = 1500,
        int checkIntervalMs = 30000)
    {
        _notificationService = notificationService;
        _logger = logger;
        _alertThresholdBytes = alertThresholdMb * 1024 * 1024;
        _criticalThresholdBytes = criticalThresholdMb * 1024 * 1024;

        _timer = new Timer(_ => CheckMemory(), null, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(checkIntervalMs));
    }

    /// <summary>
    /// 检测内存并按需处理
    /// </summary>
    private void CheckMemory()
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            var memBytes = proc.WorkingSet64;
            CurrentMemoryMb = memBytes / (1024 * 1024);

            // 严重阈值：强制 GC 回收
            if (memBytes > _criticalThresholdBytes)
            {
                _logger.Warning($"内存严重超限：{CurrentMemoryMb}MB，触发强制 GC 回收", "Memory");
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

                // 通知用户（限频：5 分钟内只通知一次）
                if ((DateTime.Now - _lastAlertTime).TotalMinutes >= 5)
                {
                    _notificationService.Notify("内存告警", $"当前内存 {CurrentMemoryMb}MB，已触发自动回收，请关注", isBuy: false);
                    _lastAlertTime = DateTime.Now;
                }
            }
            // 告警阈值：仅日志记录
            else if (memBytes > _alertThresholdBytes)
            {
                _logger.Warning($"内存偏高：{CurrentMemoryMb}MB", "Memory");
            }
        }
        catch
        {
            // 忽略检测异常
        }
    }

    /// <summary>
    /// 手动触发一次完整 GC
    /// </summary>
    public void ForceCollect()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        _logger.Info($"手动 GC 完成，当前内存 {CurrentMemoryMb}MB", "Memory");
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
