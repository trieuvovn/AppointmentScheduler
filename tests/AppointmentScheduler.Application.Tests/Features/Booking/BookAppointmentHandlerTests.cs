using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Features.Booking;
using AppointmentScheduler.Application.Tests.Common;
using AppointmentScheduler.Application.Tests.Features.Availability;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Resources;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace AppointmentScheduler.Application.Tests.Features.Booking;

public class BookAppointmentHandlerTests
{
    private static readonly Guid DealershipId = Guid.NewGuid();
    private static readonly Guid ServiceTypeId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    // A Monday in 2026, 09:00 UTC — inside the weekday dealership's 08:00-18:00 window.
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartsAt = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private static BookAppointmentRequest ARequest(DateTimeOffset? startsAt = null) => new(
        DealershipId, ServiceTypeId, VehicleId, CustomerId, startsAt ?? StartsAt);

    [Fact]
    public async Task HandleAsync_AFreeSlot_Succeeds()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");
        var technician = Technician.Create(Guid.NewGuid(), DealershipId, "Free Technician");

        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay)
            .AddTechnician(technician);

        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var result = await Handle(appointments, availability);

        result.IsSuccess.Should().BeTrue();
        result.Value.ServiceBayId.Should().Be(bay.Id);
        result.Value.TechnicianId.Should().Be(technician.Id);
        result.Value.StartsAtUtc.Should().Be(StartsAt);
        result.Value.EndsAtUtc.Should().Be(StartsAt + AnOilChange().Duration);
        result.Value.Status.Should().Be("Confirmed");
        appointments.Added.Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_AnUnknownDealership_IsDealershipNotFound()
    {
        var availability = new FakeAvailabilityRepository();
        var appointments = new FakeAppointmentRepository();

        var result = await Handle(appointments, availability);

        result.Error.Should().Be(BookingError.DealershipNotFound);
        appointments.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AnUnknownServiceType_IsServiceTypeNotFound()
    {
        var availability = new FakeAvailabilityRepository().AddDealership(AWeekdayDealership());
        var appointments = new FakeAppointmentRepository();

        var result = await Handle(appointments, availability);

        result.Error.Should().Be(BookingError.ServiceTypeNotFound);
        appointments.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AnUnknownVehicle_IsVehicleNotFound()
    {
        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange());
        var appointments = new FakeAppointmentRepository();
        // No WithVehicle call: the vehicle is unknown to this customer.

        var result = await Handle(appointments, availability);

        result.Error.Should().Be(BookingError.VehicleNotFound);
        appointments.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AStartInThePast_IsStartsInThePast()
    {
        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange());
        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var result = await Handle(appointments, availability, ARequest(Now.AddTicks(-1)));

        result.Error.Should().Be(BookingError.StartsInThePast);
    }

    [Fact]
    public async Task HandleAsync_BeyondTheBookingHorizon_IsBeyondBookingHorizon()
    {
        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange());
        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var result = await Handle(
            appointments, availability, ARequest(Now + TimeSpan.FromDays(91)));

        result.Error.Should().Be(BookingError.BeyondBookingHorizon);
    }

    [Fact]
    public async Task HandleAsync_OutsideOpeningHours_IsOutsideOpeningHours()
    {
        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange());
        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        // 19:00 is after the 18:00 closing, and still in the future relative to Now (08:00).
        var result = await Handle(
            appointments, availability, ARequest(new DateTimeOffset(2026, 3, 2, 19, 0, 0, TimeSpan.Zero)));

        result.Error.Should().Be(BookingError.OutsideOpeningHours);
    }

    [Fact]
    public async Task HandleAsync_CrossingClosingTime_IsCrossesClosingTime()
    {
        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange());
        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        // The dealership closes at 18:00; a 30-minute oil change starting 17:45 crosses it.
        var result = await Handle(
            appointments, availability, ARequest(new DateTimeOffset(2026, 3, 2, 17, 45, 0, TimeSpan.Zero)));

        result.Error.Should().Be(BookingError.CrossesClosingTime);
    }

    [Fact]
    public async Task HandleAsync_NoBayAvailable_IsNoServiceBayAvailable()
    {
        var technician = Technician.Create(Guid.NewGuid(), DealershipId, "Free Technician");

        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddTechnician(technician);
        // No bay at all.

        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var result = await Handle(appointments, availability);

        result.Error.Should().Be(BookingError.NoServiceBayAvailable);
    }

    [Fact]
    public async Task HandleAsync_NoQualifiedTechnicianAvailable_IsNoQualifiedTechnicianAvailable()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");

        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay);
        // No technician at all.

        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var result = await Handle(appointments, availability);

        result.Error.Should().Be(BookingError.NoQualifiedTechnicianAvailable);
    }

    [Fact]
    public async Task HandleAsync_TheVehicleAlreadyHasAnOverlappingAppointment_IsVehicleAlreadyBooked()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");
        var technician = Technician.Create(Guid.NewGuid(), DealershipId, "Free Technician");

        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay)
            .AddTechnician(technician);

        var existing = Appointment.Book(
            Guid.NewGuid(), DealershipId, Guid.NewGuid(), Guid.NewGuid(), AnOilChange(),
            VehicleId, CustomerId, StartsAt, Now);

        var appointments = new FakeAppointmentRepository()
            .WithVehicle(VehicleId, CustomerId)
            .WithExisting(existing);

        var result = await Handle(appointments, availability);

        result.Error.Should().Be(BookingError.VehicleAlreadyBooked);
        appointments.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ARejection_StagesNothing()
    {
        var availability = new FakeAvailabilityRepository();
        var appointments = new FakeAppointmentRepository();

        await Handle(appointments, availability);

        appointments.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AnIdempotencyKey_IsPersisted()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");
        var technician = Technician.Create(Guid.NewGuid(), DealershipId, "Free Technician");

        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay)
            .AddTechnician(technician);

        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var request = ARequest() with { IdempotencyKey = "a-replay-key" };

        await Handle(appointments, availability, request);

        appointments.Added.Single().IdempotencyKey.Should().Be("a-replay-key");
    }

    [Fact]
    public async Task HandleAsync_ASuccess_TakesTheResourceLockBeforeCheckingAvailability()
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), DealershipId, "BAY-1");
        var technician = Technician.Create(Guid.NewGuid(), DealershipId, "Free Technician");

        var availability = new FakeAvailabilityRepository()
            .AddDealership(AWeekdayDealership())
            .AddServiceType(AnOilChange())
            .AddBay(bay)
            .AddTechnician(technician);

        var appointments = new FakeAppointmentRepository().WithVehicle(VehicleId, CustomerId);

        var result = await Handle(appointments, availability);

        result.IsSuccess.Should().BeTrue();
        appointments.Calls.Should().Equal(
            nameof(IAppointmentRepository.LockResourcesAsync),
            nameof(IAppointmentRepository.HasOverlappingAppointmentAsync),
            nameof(IAppointmentRepository.Add));
    }

    private static async Task<Result<BookAppointmentResponse>> Handle(
        FakeAppointmentRepository appointments,
        FakeAvailabilityRepository availability,
        BookAppointmentRequest? request = null)
    {
        var timeProvider = new FakeTimeProvider(Now);
        var horizon = Options.Create(new BookingHorizon());
        var unitOfWork = new FakeUnitOfWork();

        var handler = new BookAppointmentHandler(
            appointments, availability, unitOfWork, timeProvider, horizon);

        return await handler.HandleAsync(
            request ?? ARequest(), TestContext.Current.CancellationToken);
    }

    private static Dealership AWeekdayDealership() => Dealership.Create(
        DealershipId, "Test Motors", "UTC",
        [Domain.Resources.OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))]);

    private static ServiceType AnOilChange() =>
        ServiceType.Create(ServiceTypeId, "OIL_CHANGE", "Oil change", 30);
}
