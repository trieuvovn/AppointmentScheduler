using AppointmentScheduler.Application.Common;

namespace AppointmentScheduler.Application.Tests.Common;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public bool Committed { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var result = await work(cancellationToken);
        Committed = true;
        return result;
    }
}
