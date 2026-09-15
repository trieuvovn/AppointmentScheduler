using System.Collections.Concurrent;

namespace AppointmentScheduler.Application.Common;

public static class TimeZoneResolver
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new();

    /// <exception cref="TimeZoneNotFoundException">The id is not recognised on this machine.</exception>
    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        return Cache.GetOrAdd(timeZoneId, static id => TimeZoneInfo.FindSystemTimeZoneById(id));
    }
}
