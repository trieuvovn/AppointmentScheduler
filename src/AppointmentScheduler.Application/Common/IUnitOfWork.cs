namespace AppointmentScheduler.Application.Common;

/// <summary>
/// The transaction boundary. Repositories stage changes; the handler decides when they commit.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Sends the staged changes to the database. Does not commit — see
    /// <see cref="ExecuteInTransactionAsync{T}"/> for why the two are kept apart.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction, retrying it whole on a transient fault, and
    /// commits on success.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
