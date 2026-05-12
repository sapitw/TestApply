namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>任务调度中枢 - 简化版，仅做内存级延迟/周期调度</summary>
public interface IKimiTaskScheduler
{
    /// <summary>延迟执行</summary>
    Guid ScheduleOnce(TimeSpan delay, Func<CancellationToken, Task> work);

    /// <summary>周期执行（首次延迟 + 周期）</summary>
    Guid SchedulePeriodic(TimeSpan firstDelay, TimeSpan period, Func<CancellationToken, Task> work);

    /// <summary>取消调度任务</summary>
    bool Cancel(Guid scheduleId);
}
