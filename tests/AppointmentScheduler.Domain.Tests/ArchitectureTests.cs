using System.Reflection;
using AppointmentScheduler.Domain.Appointments;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests;

public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Appointment).Assembly;

    [Fact]
    public void GetReferencedAssemblies_DomainAssembly_DoesNotContainEntityFrameworkCore()
    {
        Domain.GetReferencedAssemblies()
              .Should().NotContain(a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void GetReferencedAssemblies_DomainAssembly_DoesNotContainAnyOtherSolutionProject()
    {
        Domain.GetReferencedAssemblies()
              .Should().NotContain(a => a.Name!.StartsWith("AppointmentScheduler.", StringComparison.Ordinal));
    }

    [Fact]
    public void GetReferencedAssemblies_DomainAssembly_ContainsNothingOutsideTheBaseClassLibrary()
    {
        Domain.GetReferencedAssemblies()
              .Select(a => a.Name!)
              .Should().OnlyContain(name =>
                  name.StartsWith("System.", StringComparison.Ordinal) ||
                  name == "netstandard" ||
                  name == "mscorlib");
    }
}
