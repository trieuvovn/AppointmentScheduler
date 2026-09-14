using System.Reflection;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Application.Tests;

public class ArchitectureTests
{
    private static readonly Assembly Application = typeof(AssemblyMarker).Assembly;

    [Fact]
    public void GetReferencedAssemblies_ApplicationAssembly_DoesNotContainEntityFrameworkCore()
    {
        Application.GetReferencedAssemblies()
                   .Should().NotContain(a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void GetReferencedAssemblies_ApplicationAssembly_DoesNotContainInfrastructureOrApi()
    {
        var forbidden = new[] { "AppointmentScheduler.Infrastructure", "AppointmentScheduler.Api" };

        Application.GetReferencedAssemblies()
                   .Select(a => a.Name!)
                   .Should().NotIntersectWith(forbidden);
    }
}
