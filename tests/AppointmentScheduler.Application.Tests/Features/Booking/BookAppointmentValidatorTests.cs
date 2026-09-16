using AppointmentScheduler.Application.Features.Booking;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Application.Tests.Features.Booking;

public class BookAppointmentValidatorTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly BookAppointmentValidator _validator = new();

    private static BookAppointmentRequest AValidRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StartsAt);

    [Fact]
    public void Validate_AWellFormedRequest_IsValid()
    {
        var result = _validator.Validate(AValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AnEmptyDealershipId_IsInvalid()
    {
        var result = _validator.Validate(AValidRequest() with { DealershipId = Guid.Empty });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AnEmptyServiceTypeId_IsInvalid()
    {
        var result = _validator.Validate(AValidRequest() with { ServiceTypeId = Guid.Empty });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AnEmptyVehicleId_IsInvalid()
    {
        var result = _validator.Validate(AValidRequest() with { VehicleId = Guid.Empty });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AnEmptyCustomerId_IsInvalid()
    {
        var result = _validator.Validate(AValidRequest() with { CustomerId = Guid.Empty });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AStartsAtWithANonZeroOffset_IsInvalid()
    {
        var withOffset = StartsAt.ToOffset(TimeSpan.FromHours(7));

        var result = _validator.Validate(AValidRequest() with { StartsAtUtc = withOffset });

        result.IsValid.Should().BeFalse();
    }
}
