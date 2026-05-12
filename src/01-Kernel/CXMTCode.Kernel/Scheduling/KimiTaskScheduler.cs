using System.Collections.Concurrent;
using CXMTCode.Kernel.Contracts.Interfaces;
using Microsoft.Extensions.Logging;

namespace CXMTCode.Kernel.Scheduling;

/// <summary>
/// 任务调度中枢 - 简化内存级实现。
/// 用 Timer + 字典登记任务，进程级有效，不持久化。
/// 生产环境建议替换为 Quartz.NET / Hangfire。
/// </summary>
public sealed class KimiTaskScheduler : IKimiTaskScheduler, IDisposable
{
    private readonly ILogger<KimiTaskScheduler>? _log;
    private readonly ConcurrentDictionary<Guid, ScheduledItem> _items = new();

    public KimiTaskScheduler(ILogger<KimiTaskScheduler>? log = null) => _log = log;

    public Guid ScheduleOnce(TimeSpan delay, Func<CancellationToken, Task> work)
    {
        var id  = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var timer = new Timer(async _ =>
        {
            try { await work(cts.Token); }
            catch (Exception ex) { _log?.LogError(ex, "Scheduled task {Id} threw", id); }
            finally { Cancel(id); }
        }, null, delay, Timeout.InfiniteTimeSpan);
        _items[id] = new ScheduledItem(timer, cts);
        return id;
    }

    public Guid SchedulePeriodic(TimeSpan firstDelay, TimeSpan period, Func<CancellationToken, Task> work)
    {
        var id  = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var timer = new Timer(async _ =>
        {
            try { await work(cts.Token); }
            catch (Exception ex) { _log?.LogError(ex, "Periodic task {Id} threw", id); }
        }, null, firstDelay, period);
        _items[id] = new ScheduledItem(timer, cts);
        return id;
    }

    public bool Cancel(Guid scheduleId)
    {
        if (!_items.TryRemove(scheduleId, out var item)) return false;
        item.Cts.Cancel();
        item.Timer.Dispose();
        item.Cts.Dispose();
        return true;
    }

    public void Dispose()
    {
        foreach (var id in _items.Keys.ToList()) Cancel(id);
    }

    private sealed record ScheduledItem(Timer Timer, CancellationTokenSource Cts);
}
