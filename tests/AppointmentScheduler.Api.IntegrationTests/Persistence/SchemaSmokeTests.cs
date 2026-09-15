using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class SchemaSmokeTests
{
    private readonly DatabaseFixture _fixture;

    public SchemaSmokeTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task EfModel_EveryMappedEntity_MatchesTheMigratedSchema()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var query = async () =>
        {
            await db.Dealerships.Take(1).ToListAsync(ct);
            await db.ServiceBays.Take(1).ToListAsync(ct);
            await db.Technicians.Take(1).ToListAsync(ct);
            await db.Skills.Take(1).ToListAsync(ct);
            await db.ServiceTypes.Take(1).ToListAsync(ct);
            await db.Customers.Take(1).ToListAsync(ct);
            await db.Vehicles.Take(1).ToListAsync(ct);
            await db.Appointments.Take(1).ToListAsync(ct);
        };

        await query.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EfModel_OwnedCollections_MapOntoTheirLinkTables()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var query = async () =>
        {
            await db.Dealerships.Take(1).ToListAsync(ct);
            await db.Technicians.Take(1).ToListAsync(ct);
            await db.ServiceTypes.Take(1).ToListAsync(ct);
        };

        await query.Should().NotThrowAsync();
    }
}
