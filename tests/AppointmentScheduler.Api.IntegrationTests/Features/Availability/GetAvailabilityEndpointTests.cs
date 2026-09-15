using System.Net;
using System.Net.Http.Json;
using AppointmentScheduler.Application.Features.Availability;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Features.Availability;

[Collection(DatabaseCollection.Name)]
public class GetAvailabilityEndpointTests : IDisposable
{
    private static readonly Guid DealershipId = Guid.Parse("1a0b0000-0000-4000-8000-00000000000d");
    private static readonly Guid OilChangeId = Guid.Parse("3c2b0d50-0000-4000-8000-000000000001");
    private static readonly Guid EvBatteryCheckId = Guid.Parse("3c2b0d50-0000-4000-8000-000000000007");

    private readonly ApiFactory _factory;

    public GetAvailabilityEndpointTests(DatabaseFixture fixture)
    {
        fixture.SeedDemoData();
        _factory = new ApiFactory(fixture);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Get_ASeededMonday_ReturnsOkWithBookableSlots()
    {
        using var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(
            $"/api/v1/availability?dealershipId={DealershipId}&serviceTypeId={OilChangeId}&date=2026-03-02", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetAvailabilityResponse>(ct);

        body!.OpensAtUtc.Should().NotBeNull();
        body.Slots.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_ASunday_ReturnsOkWithNullOpensAtAndNoSlots()
    {
        using var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // 2026-03-01 is a Sunday; the demo dealership has no row for it, so it is closed.
        using var response = await client.GetAsync(
            $"/api/v1/availability?dealershipId={DealershipId}&serviceTypeId={OilChangeId}&date=2026-03-01", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetAvailabilityResponse>(ct);

        body!.OpensAtUtc.Should().BeNull();
        body.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_ASaturdayForAnEvBatteryCheck_OnlyTheOpeningSlotFits()
    {
        using var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // 2026-03-07 is a Saturday; the demo dealership's 09:00-13:00 window fits a 240-minute
        // service only if it starts exactly at opening (09:00 + 240m = 13:00, the window's own
        // end) — every later candidate crosses closing.
        using var response = await client.GetAsync(
            $"/api/v1/availability?dealershipId={DealershipId}&serviceTypeId={EvBatteryCheckId}&date=2026-03-07", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetAvailabilityResponse>(ct);

        body!.OpensAtUtc.Should().NotBeNull();
        body.Slots.Should().ContainSingle();
        body.Slots[0].StartsAtUtc.Should().Be(body.OpensAtUtc);
    }

    [Fact]
    public async Task Get_AnUnknownDealership_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(
            $"/api/v1/availability?dealershipId={Guid.NewGuid()}&serviceTypeId={OilChangeId}&date=2026-03-02", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
