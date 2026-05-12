namespace CXMTCode.Kernel.Contracts.Interfaces;

/// <summary>分布式事务中枢 - 简化版 Saga 模式</summary>
public interface IDistributedTransaction
{
    /// <summary>开启 Saga 事务</summary>
    Task<IDistributedTransactionScope> BeginAsync(string transactionName);
}

public interface IDistributedTransactionScope : IAsyncDisposable
{
    Guid TransactionId { get; }
    string TransactionName { get; }

    /// <summary>登记补偿动作（按 LIFO 在回滚时调用）</summary>
    void RegisterCompensation(Func<Task> compensate);

    Task CommitAsync();
    Task RollbackAsync(string? reason = null);
}
