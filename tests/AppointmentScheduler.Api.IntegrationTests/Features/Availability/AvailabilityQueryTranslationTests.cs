using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;
using AppointmentScheduler.Infrastructure.Persistence;
using AppointmentScheduler.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Features.Availability;

[Collection(DatabaseCollection.Name)]
public class AvailabilityQueryTranslationTests
{
    private static readonly TimeSlot Window = TimeSlot.Create(
        new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 3, 2, 23, 59, 0, TimeSpan.Zero));

    private readonly DatabaseFixture _fixture;

    public AvailabilityQueryTranslationTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetQualifiedTechnicianOccupancyAsync_SkillSubsetAndOrdering_TranslateWithoutClientEvaluation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateContext();

        var dealership = Dealership.Create(
            Guid.NewGuid(), "Translation Motors", "Europe/London",
            [Domain.Resources.OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(0, 0), new TimeOnly(23, 59))]);
        db.Dealerships.Add(dealership);

        var brakes = Skill.Create(Guid.NewGuid(), NewCode("BRAKES"), "Brakes");
        var tyres = Skill.Create(Guid.NewGuid(), NewCode("TYRES"), "Tyres");
        db.Skills.AddRange(brakes, tyres);

        var serviceType = ServiceType.Create(
            Guid.NewGuid(), NewCode("SVC"), "Brake and tyre service", 60, requiredSkills: [brakes, tyres]);
        db.ServiceTypes.Add(serviceType);

        // Holds only one of the two required skills — must be excluded by the subset test.
        var partiallyQualified = Technician.Create(
            Guid.NewGuid(), dealership.Id, "Partially Qualified", skills: [brakes]);

        // Holds both, plus an extra unrelated skill — must be included despite the extra.
        var extra = Skill.Create(Guid.NewGuid(), NewCode("EXTRA"), "Extra");
        db.Skills.Add(extra);
        var fullyQualified = Technician.Create(
            Guid.NewGuid(), dealership.Id, "Fully Qualified", skills: [brakes, tyres, extra]);

        db.Technicians.AddRange(partiallyQualified, fullyQualified);
        await db.SaveChangesAsync(ct);

        var repository = new AvailabilityRepository(db);

        var query = async () => await repository.GetQualifiedTechnicianOccupancyAsync(
            dealership.Id, serviceType.Id, Window, ct);

        var occupancy = await query.Should().NotThrowAsync();

        occupancy.Subject.Select(o => o.ResourceId).Should().BeEquivalentTo([fullyQualified.Id]);
    }

    private static string NewCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
}
