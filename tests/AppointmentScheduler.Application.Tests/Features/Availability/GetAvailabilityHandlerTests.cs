using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Application.Tests.Features.Availability;

public class GetAvailabilityHandlerTests
{
    private static readonly Guid DealershipId = Guid.NewGuid();
    private static readonly Guid ServiceTypeId = Guid.NewGuid();

    // A Monday in 2026, chosen only for a stable DayOfWeek in this fixed-offset test setup.
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Sunday = new(2026, 3, 1);

    [Fact]
    public async Task HandleAsync_AnUnknownDealership_IsDealershipNotFound()
    {
        var repository = new FakeAvailabilityRepository();
        var handler = new GetAvailabilityHandler(repository);

        var result = await handler.HandleAsync(
            new GetAvailabilityRequest(DealershipId, ServiceTypeId, Monday), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingError.DealershipNotFound);
    }

    [Fact]
    public async Task HandleAsync_AnUnknownServiceType_IsServiceTypeNotFound()
    {
        var repository = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership());
        var handler = new GetAvailabilityHandler(repository);

        var result = await handler.HandleAsync(
            new GetAvailabilityRequest(DealershipId, ServiceTypeId, Monday), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingError.ServiceTypeNotFound);
    }

    [Fact]
    public async Task HandleAsync_AClosedDay_ReturnsNullOpensAtAndNoSlots()
    {
        var repository = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange());
        var handler = new GetAvailabilityHandler(repository);

        var result = await handler.HandleAsync(
            new GetAvailabilityRequest(DealershipId, ServiceTypeId, Sunday), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.OpensAtUtc.Should().BeNull();
        result.Value.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AnOpenDayWithNoCandidateFree_ReturnsOpensAtWithEmptySlots()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");

        var repository = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay);
            // No technician at all: every candidate slot fails with NoQualifiedTechnicianAvailable.

        var handler = new GetAvailabilityHandler(repository);

        var result = await handler.HandleAsync(
            new GetAvailabilityRequest(DealershipId, ServiceTypeId, Monday), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.OpensAtUtc.Should().NotBeNull();
        result.Value.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AnOpenDayWithFreeResources_ReturnsBookableSlots()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");
        var technician = Technician.Create(Guid.NewGuid(), DealershipId, "Free Technician");

        var repository = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay)
            .AddTechnician(technician);

        var handler = new GetAvailabilityHandler(repository);

        var result = await handler.HandleAsync(
            new GetAvailabilityRequest(DealershipId, ServiceTypeId, Monday), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Slots.Should().NotBeEmpty();
    }

    private static Dealership AWeekdayDealership() => Dealership.Create(
        DealershipId, "Test Motors", "Europe/London",
        [Domain.Resources.OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))]);

    private static ServiceType AnOilChange() =>
        ServiceType.Create(ServiceTypeId, "OIL_CHANGE", "Oil change", 30);
}
