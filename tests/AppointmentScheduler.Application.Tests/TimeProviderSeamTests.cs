using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace AppointmentScheduler.Application.Tests;

public class TimeProviderSeamTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetUtcNow_AFakeTimeProvider_ReturnsTheInstantItWasSetTo()
    {
        var time = new FakeTimeProvider(Fixed);

        time.GetUtcNow().Should().Be(Fixed);
    }

    [Fact]
    public void GetUtcNow_AfterAdvancing_ReturnsTheAdvancedInstant()
    {
        var time = new FakeTimeProvider(Fixed);

        time.Advance(TimeSpan.FromDays(91));

        // The far side of a ninety-day horizon, reached without waiting ninety days.
        time.GetUtcNow().Should().Be(Fixed.AddDays(91));
    }

    [Fact]
    public void GetUtcNow_TimeProviderSystem_IsTheProductionImplementation()
    {
        TimeProvider.System.GetUtcNow().Offset.Should().Be(TimeSpan.Zero);
    }
}
