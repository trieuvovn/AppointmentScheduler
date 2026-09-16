namespace AppointmentScheduler.Infrastructure;

/// <summary>SQL Server connection tuning. Bound from the <see cref="SectionName"/> config section.</summary>
public sealed record SqlServerOptions
{
    public const string SectionName = "SqlServer";

    public int CommandTimeoutSeconds { get; init; } = 30;
}
