using System.Reflection;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests;

public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(AssemblyMarker).Assembly;

    [Fact]
    public void Domain_does_not_reference_EntityFrameworkCore()
    {
        Domain.GetReferencedAssemblies()
              .Should().NotContain(a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_does_not_reference_any_other_solution_project()
    {
        Domain.GetReferencedAssemblies()
              .Should().NotContain(a => a.Name!.StartsWith("AppointmentScheduler.", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_references_nothing_outside_the_base_class_library()
    {
        Domain.GetReferencedAssemblies()
              .Select(a => a.Name!)
              .Should().OnlyContain(name =>
                  name.StartsWith("System.", StringComparison.Ordinal) ||
                  name == "netstandard" ||
                  name == "mscorlib");
    }
}
