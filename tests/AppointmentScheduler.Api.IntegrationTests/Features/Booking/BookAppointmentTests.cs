using System.Net;
using System.Net.Http.Json;
using AppointmentScheduler.Application.Features.Availability;
using AppointmentScheduler.Application.Features.Booking;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Features.Booking;

[Collection(DatabaseCollection.Name)]
public class BookAppointmentTests : IDisposable
{
    // 2026-09-21 is a Monday inside the booking horizon, inside the seeded 08:00-18:00 UTC window.
    private static readonly DateTimeOffset StartsAt = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private readonly ApiFactory _factory;

    public BookAppointmentTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Post_AFreeSlot_ReturnsOkAndLandsInTheDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        BookingScenario scenario;

        await using (var db = _fixture.CreateContext())
        {
            scenario = await BookingSeed.BuildAsync(db, ct);
        }

        using var client = _factory.CreateClient();

        var request = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, scenario.Vehicle.Id, scenario.Customer.Id, StartsAt);

        using var response = await client.PostAsJsonAsync("/api/v1/appointments", request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BookAppointmentResponse>(ct);
        body!.ServiceBayId.Should().Be(scenario.Bay.Id);
        body.TechnicianId.Should().Be(scenario.Technician.Id);
        body.Status.Should().Be("Confirmed");

        await using var reading = _fixture.CreateContext();
        var stored = await reading.Appointments.SingleAsync(a => a.Id == body.AppointmentId, ct);

        stored.VehicleId.Should().Be(scenario.Vehicle.Id);
        stored.StartsAtUtc.Should().Be(StartsAt);
    }

    [Fact]
    public async Task Post_TheSameSlotTwice_TheSecondIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        BookingScenario scenario;

        await using (var db = _fixture.CreateContext())
        {
            scenario = await BookingSeed.BuildAsync(db, ct);
        }

        using var client = _factory.CreateClient();

        var first = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, scenario.Vehicle.Id, scenario.Customer.Id, StartsAt);

        using var firstResponse = await client.PostAsJsonAsync("/api/v1/appointments", first, ct);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A second vehicle for the same customer, same slot — the bay and technician are what
        // collide this time, not the vehicle-overlap check.
        Domain.Customers.Vehicle secondVehicle;
        await using (var db = _fixture.CreateContext())
        {
            secondVehicle = Domain.Customers.Vehicle.Create(
                Guid.NewGuid(), scenario.Customer.Id,
                $"{Guid.NewGuid():N}".ToUpperInvariant()[..17], "Honda", "Civic");
            db.Vehicles.Add(secondVehicle);
            await db.SaveChangesAsync(ct);
        }

        var second = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, secondVehicle.Id, scenario.Customer.Id, StartsAt);

        using var secondResponse = await client.PostAsJsonAsync("/api/v1/appointments", second, ct);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_TheSameVehicleInTwoBays_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        BookingScenario scenario;

        await using (var db = _fixture.CreateContext())
        {
            scenario = await BookingSeed.BuildAsync(db, ct);
        }

        using var client = _factory.CreateClient();

        var first = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, scenario.Vehicle.Id, scenario.Customer.Id, StartsAt);

        using var firstResponse = await client.PostAsJsonAsync("/api/v1/appointments", first, ct);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A second bay and technician are free, but the same vehicle cannot be in two places at once.
        await using (var db = _fixture.CreateContext())
        {
            await BookingSeed.AddBayAsync(db, scenario.Dealership.Id, ct);
            await BookingSeed.AddTechnicianAsync(db, scenario.Dealership.Id, ct);
        }

        var second = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, scenario.Vehicle.Id, scenario.Customer.Id,
            StartsAt.AddMinutes(15));

        using var secondResponse = await client.PostAsJsonAsync("/api/v1/appointments", second, ct);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var reading = _fixture.CreateContext();
        var count = await reading.Appointments.CountAsync(a => a.VehicleId == scenario.Vehicle.Id, ct);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Post_ABookedSlot_StopsAppearingInAvailability()
    {
        var ct = TestContext.Current.CancellationToken;
        BookingScenario scenario;

        await using (var db = _fixture.CreateContext())
        {
            scenario = await BookingSeed.BuildAsync(db, ct);
        }

        using var client = _factory.CreateClient();

        var localDate = DateOnly.FromDateTime(StartsAt.UtcDateTime);

        using var before = await client.GetAsync(
            $"/api/v1/availability?dealershipId={scenario.Dealership.Id}&serviceTypeId={scenario.ServiceType.Id}&date={localDate:yyyy-MM-dd}",
            ct);
        var beforeBody = await before.Content.ReadFromJsonAsync<GetAvailabilityResponse>(ct);
        beforeBody!.Slots.Should().Contain(s => s.StartsAtUtc == StartsAt);

        var request = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, scenario.Vehicle.Id, scenario.Customer.Id, StartsAt);
        using var bookResponse = await client.PostAsJsonAsync("/api/v1/appointments", request, ct);
        bookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var after = await client.GetAsync(
            $"/api/v1/availability?dealershipId={scenario.Dealership.Id}&serviceTypeId={scenario.ServiceType.Id}&date={localDate:yyyy-MM-dd}",
            ct);
        var afterBody = await after.Content.ReadFromJsonAsync<GetAvailabilityResponse>(ct);
        afterBody!.Slots.Should().NotContain(s => s.StartsAtUtc == StartsAt);
    }

    [Fact]
    public async Task Post_AMalformedRequest_ReturnsValidationProblem()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var request = new BookAppointmentRequest(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StartsAt);

        using var response = await client.PostAsJsonAsync("/api/v1/appointments", request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_AnIdempotencyKeyHeader_IsPersistedOnTheRow()
    {
        var ct = TestContext.Current.CancellationToken;
        BookingScenario scenario;

        await using (var db = _fixture.CreateContext())
        {
            scenario = await BookingSeed.BuildAsync(db, ct);
        }

        using var client = _factory.CreateClient();

        var request = new BookAppointmentRequest(
            scenario.Dealership.Id, scenario.ServiceType.Id, scenario.Vehicle.Id, scenario.Customer.Id, StartsAt);

        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(httpRequest, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BookAppointmentResponse>(ct);

        await using var reading = _fixture.CreateContext();
        var stored = await reading.Appointments.SingleAsync(a => a.Id == body!.AppointmentId, ct);

        stored.IdempotencyKey.Should().Be(idempotencyKey);
    }
}
