using AppointmentScheduler.Application.Common;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Application.Tests.Common;

public class TimeZoneResolverTests
{
    [Fact]
    public void Resolve_AnIanaId_ResolvesNatively()
    {
        // Verified up front (stage3.md): IANA ids resolve via ICU since .NET 6. A container built
        // with InvariantGlobalization=true would break this at runtime — this test is the guard.
        var timeZone = TimeZoneResolver.Resolve("Europe/London");

        timeZone.Id.Should().Be("Europe/London");
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsTheSameCachedInstance()
    {
        var first = TimeZoneResolver.Resolve("Asia/Ho_Chi_Minh");
        var second = TimeZoneResolver.Resolve("Asia/Ho_Chi_Minh");

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Resolve_AnUnknownId_Throws()
    {
        var resolve = () => TimeZoneResolver.Resolve("Not/A_Real_Zone");

        resolve.Should().Throw<TimeZoneNotFoundException>();
    }
}
