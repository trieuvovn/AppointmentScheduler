using System.Reflection;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Application.Tests;

public class ArchitectureTests
{
    private static readonly Assembly Application = typeof(AssemblyMarker).Assembly;

    [Fact]
    public void Application_does_not_reference_EntityFrameworkCore()
    {
        Application.GetReferencedAssemblies()
                   .Should().NotContain(a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_Api()
    {
        var forbidden = new[] { "AppointmentScheduler.Infrastructure", "AppointmentScheduler.Api" };

        Application.GetReferencedAssemblies()
                   .Select(a => a.Name!)
                   .Should().NotIntersectWith(forbidden);
    }
}
