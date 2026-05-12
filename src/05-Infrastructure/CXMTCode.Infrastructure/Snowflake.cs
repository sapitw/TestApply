namespace CXMTCode.Infrastructure;

/// <summary>
/// 简化版雪花算法 ID 生成器。
/// 时间戳(41) | WorkerId(10) | Sequence(12) = 63 bits（高位 0）。
/// </summary>
public sealed class SnowflakeIdGenerator
{
    private readonly long _workerId;
    private long _lastTimestamp = -1L;
    private long _sequence;
    private readonly object _lock = new();

    private const long Epoch = 1700000000000L; // 2023-11-14
    private const int WorkerBits   = 10;
    private const int SequenceBits = 12;
    private const long MaxSequence = (1L << SequenceBits) - 1;
    private const int WorkerShift  = SequenceBits;
    private const int TimestampShift = WorkerBits + SequenceBits;

    public SnowflakeIdGenerator(long workerId = 1)
    {
        if (workerId < 0 || workerId > ((1L << WorkerBits) - 1))
            throw new ArgumentOutOfRangeException(nameof(workerId));
        _workerId = workerId;
    }

    public long NextId()
    {
        lock (_lock)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (ts == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                {
                    while ((ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) <= _lastTimestamp) { }
                }
            }
            else
            {
                _sequence = 0;
            }
            _lastTimestamp = ts;
            return ((ts - Epoch) << TimestampShift) | (_workerId << WorkerShift) | _sequence;
        }
    }
}
