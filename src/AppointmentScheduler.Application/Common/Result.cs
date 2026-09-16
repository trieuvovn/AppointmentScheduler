using AppointmentScheduler.Domain.Appointments;

namespace AppointmentScheduler.Application.Common;

/// <summary>A use case's outcome with no value: either it succeeded, or it failed for a <see cref="BookingError"/>.</summary>
public readonly record struct Result
{
    private Result(bool isSuccess, BookingError? error, string? detail)
    {
        IsSuccess = isSuccess;
        Error = error;
        Detail = detail;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Set when <see cref="IsFailure"/>; null on success.</summary>
    public BookingError? Error { get; }

    public string? Detail { get; }

    public static Result Success() => new(true, null, null);

    public static Result Failure(BookingError error, string? detail = null) => new(false, error, detail);

    public static Result<T> Success<T>(T value) => Result<T>.Succeeded(value);

    public static Result<T> Failure<T>(BookingError error, string? detail = null) =>
        Result<T>.Failed(error, detail);
}

/// <summary>A use case's outcome carrying a value on success, or a <see cref="BookingError"/> on failure.</summary>
public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, BookingError? error, string? detail)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
        Detail = detail;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Set when <see cref="IsSuccess"/>; throws otherwise.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>Set when <see cref="IsFailure"/>; null on success.</summary>
    public BookingError? Error { get; }

    /// <summary>Optional phrasing for the failure. See <see cref="Result.Detail"/>.</summary>
    public string? Detail { get; }

    internal static Result<T> Succeeded(T value) => new(true, value, null, null);

    internal static Result<T> Failed(BookingError error, string? detail = null) =>
        new(false, default, error, detail);
}
