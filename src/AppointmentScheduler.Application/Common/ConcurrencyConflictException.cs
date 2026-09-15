namespace AppointmentScheduler.Application.Common;

/// <summary>
/// Thrown when a write lost an optimistic-concurrency race: the row's version moved on before the
/// update landed.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was modified by another transaction.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
