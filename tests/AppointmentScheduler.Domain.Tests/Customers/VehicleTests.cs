using AppointmentScheduler.Domain.Customers;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Domain.Tests.Customers;

public class VehicleTests
{
    private const string ValidVin = "1HGBH41JXMN109186";

    private static Vehicle Create(string vin = ValidVin, string? licensePlate = null) =>
        Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), vin, "Honda", "Civic", licensePlate);

    [Fact]
    public void Create_ValidRequest_KeepsTheDetailsItWasGiven()
    {
        var vehicle = Create(licensePlate: "AB12 CDE");

        vehicle.Vin.Should().Be(ValidVin);
        vehicle.Make.Should().Be("Honda");
        vehicle.Model.Should().Be("Civic");
        vehicle.LicensePlate.Should().Be("AB12 CDE");
    }

    [Fact]
    public void Create_NoLicensePlateGiven_LeavesItNull()
    {
        // A newly delivered car has none until it is registered (data model 3.7).
        Create().LicensePlate.Should().BeNull();
    }

    [Theory]
    [InlineData("TOOSHORT")]
    [InlineData("THISVINISWAYTOOLONG")]
    public void Create_VinNotSeventeenCharacters_ThrowsArgumentException(string vin)
    {
        var act = () => Create(vin);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_VinExactlySeventeenCharacters_DoesNotThrow()
    {
        ValidVin.Should().HaveLength(Vehicle.VinLength);

        var act = () => Create();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingVin_ThrowsArgumentException(string? vin)
    {
        var act = () => Create(vin!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_MissingMake_ThrowsArgumentException()
    {
        var act = () => Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), ValidVin, "", "Civic");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_MissingModel_ThrowsArgumentException()
    {
        var act = () => Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), ValidVin, "Honda", "");

        act.Should().Throw<ArgumentException>();
    }
}
