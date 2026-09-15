using AppointmentScheduler.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppointmentDbContext _db;

    public UnitOfWork(AppointmentDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var strategy = _db.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var result = await work(ct);

            await transaction.CommitAsync(ct);

            return result;
        }, cancellationToken);
    }
}
