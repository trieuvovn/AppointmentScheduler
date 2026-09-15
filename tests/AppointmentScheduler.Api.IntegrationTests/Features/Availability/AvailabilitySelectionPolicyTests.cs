using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Features.Availability;

[Collection(DatabaseCollection.Name)]
public class AvailabilitySelectionPolicyTests
{
    private static readonly Guid DealershipId = Guid.Parse("1a0b0000-0000-4000-8000-00000000000d");
    private static readonly Guid OilChangeId = Guid.Parse("3c2b0d50-0000-4000-8000-000000000001");
    private static readonly Guid EvBatteryCheckId = Guid.Parse("3c2b0d50-0000-4000-8000-000000000007");
    private static readonly Guid AlinaId = Guid.Parse("5e4d0f70-0000-4000-8000-000000000001");
    private static readonly Guid DagnyId = Guid.Parse("5e4d0f70-0000-4000-8000-000000000004");

    // A Monday, well inside the seeded opening hours, with no demo appointments booked against it.
    private static readonly TimeSlot AMondayWindow = TimeSlot.Create(
        new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 3, 2, 18, 0, 0, TimeSpan.Zero));

    private readonly DatabaseFixture _fixture;

    public AvailabilitySelectionPolicyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.SeedDemoData();
    }

    [Fact]
    public async Task GetQualifiedTechnicianOccupancyAsync_OilChange_OrdersAlinaFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateContext();
        var repository = new AvailabilityRepository(db);

        var technicians = await repository.GetQualifiedTechnicianOccupancyAsync(
            DealershipId, OilChangeId, AMondayWindow, ct);

        technicians.Should().NotBeEmpty();
        technicians[0].ResourceId.Should().Be(AlinaId);
    }

    [Fact]
    public async Task GetQualifiedTechnicianOccupancyAsync_EvBatteryCheck_OnlyDagnyQualifies()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateContext();
        var repository = new AvailabilityRepository(db);

        var technicians = await repository.GetQualifiedTechnicianOccupancyAsync(
            DealershipId, EvBatteryCheckId, AMondayWindow, ct);

        technicians.Select(t => t.ResourceId).Should().BeEquivalentTo([DagnyId]);
    }
}
