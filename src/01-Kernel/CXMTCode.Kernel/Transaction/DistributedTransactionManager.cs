using CXMTCode.Kernel.Contracts.Interfaces;
using Microsoft.Extensions.Logging;

namespace CXMTCode.Kernel.Transaction;

/// <summary>
/// 分布式事务中枢 - Saga 模式简化实现。
/// 业务方在每个步骤后登记补偿动作；Rollback 时按 LIFO 顺序调用。
/// </summary>
public sealed class DistributedTransactionManager : IDistributedTransaction
{
    private readonly ILogger<DistributedTransactionManager>? _log;
    public DistributedTransactionManager(ILogger<DistributedTransactionManager>? log = null) => _log = log;

    public Task<IDistributedTransactionScope> BeginAsync(string transactionName)
        => Task.FromResult<IDistributedTransactionScope>(new SagaScope(transactionName, _log));

    private sealed class SagaScope : IDistributedTransactionScope
    {
        private readonly ILogger? _log;
        private readonly Stack<Func<Task>> _compensations = new();
        private bool _committed;

        public Guid   TransactionId   { get; } = Guid.NewGuid();
        public string TransactionName { get; }

        public SagaScope(string name, ILogger? log) { TransactionName = name; _log = log; }

        public void RegisterCompensation(Func<Task> compensate) => _compensations.Push(compensate);

        public Task CommitAsync()
        {
            _committed = true;
            _compensations.Clear();
            return Task.CompletedTask;
        }

        public async Task RollbackAsync(string? reason = null)
        {
            _log?.LogWarning("Saga {Tx} rollback: {Reason}", TransactionName, reason);
            while (_compensations.Count > 0)
            {
                var act = _compensations.Pop();
                try { await act(); }
                catch (Exception ex) { _log?.LogError(ex, "Compensation failed in saga {Tx}", TransactionName); }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed && _compensations.Count > 0)
                await RollbackAsync("Disposed without Commit");
        }
    }
}
