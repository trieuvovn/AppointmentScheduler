using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Customers;
using AppointmentScheduler.Domain.Resources;
using AppointmentScheduler.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class PersistenceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;

    public PersistenceTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveChanges_ADealershipWithOpeningHours_RoundTripsEveryDay()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var dealership = Dealership.Create(
            Guid.NewGuid(),
            "Round Trip Motors",
            "Asia/Ho_Chi_Minh",
            [
                OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                OpeningHours.Create(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            ]);

        db.Dealerships.Add(dealership);
        await db.SaveChangesAsync(ct);

        await using var reading = _fixture.CreateContext();

        var stored = await reading.Dealerships
            .SingleAsync(d => d.Id == dealership.Id, ct);

        stored.TimeZoneId.Should().Be("Asia/Ho_Chi_Minh");
        stored.OpeningHours.Should().HaveCount(2);
        stored.HoursOn(DayOfWeek.Monday)!.Value.ClosesAt.Should().Be(new TimeOnly(17, 0));
        stored.HoursOn(DayOfWeek.Sunday).Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_ATechnicianWithSkills_RoundTripsThroughTheLinkTable()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var dealership = await AddDealershipAsync(db, ct);
        var brakes = await AddSkillAsync(db, ct);
        var diagnostics = await AddSkillAsync(db, ct);

        var technician = Technician.Create(
            Guid.NewGuid(), dealership.Id, "Ana Pham", skills: [brakes, diagnostics]);

        db.Technicians.Add(technician);
        await db.SaveChangesAsync(ct);

        await using var reading = _fixture.CreateContext();

        // Include: a real navigation is not loaded automatically, unlike an owned collection.
        var stored = await reading.Technicians
            .Include(t => t.Skills)
            .SingleAsync(t => t.Id == technician.Id, ct);

        stored.Skills.Select(s => s.Id).Should().BeEquivalentTo(new[] { brakes.Id, diagnostics.Id });
    }

    [Fact]
    public async Task SaveChanges_AServiceTypeWithRequiredSkills_RoundTripsThroughTheLinkTable()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var skill = await AddSkillAsync(db, ct);

        var serviceType = ServiceType.Create(
            Guid.NewGuid(), NewCode("SVC"), "Brake service", 90, requiredSkills: [skill]);

        db.ServiceTypes.Add(serviceType);
        await db.SaveChangesAsync(ct);

        await using var reading = _fixture.CreateContext();

        var stored = await reading.ServiceTypes
            .Include(s => s.RequiredSkills)
            .SingleAsync(s => s.Id == serviceType.Id, ct);

        stored.RequiredSkills.Select(s => s.Id).Should().BeEquivalentTo(new[] { skill.Id });
        stored.Duration.Should().Be(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public async Task SaveChanges_AnAppointment_StoresItsStatusAsAName()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var appointment = await AddAppointmentAsync(db, ct);

        var status = await db.Database
            .SqlQuery<string>($"SELECT Status AS Value FROM Appointments WHERE Id = {appointment.Id}")
            .SingleAsync(ct);

        status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task SaveChanges_AnAppointment_RoundTripsItsInstantsAsUtc()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var appointment = await AddAppointmentAsync(db, ct);

        await using var reading = _fixture.CreateContext();

        var stored = await reading.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);

        stored.StartsAtUtc.Offset.Should().Be(TimeSpan.Zero);
        stored.StartsAtUtc.Should().Be(Noon);
        stored.EndsAtUtc.Should().Be(Noon.AddMinutes(60));
    }

    [Fact]
    public async Task SaveChanges_AnInstantGivenWithANonZeroOffset_StoresTheSameMoment()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        // Noon UTC expressed as a +07:00 wall-clock time — the same instant, different offset.
        var withPositiveOffset = Noon.ToOffset(TimeSpan.FromHours(7));
        var appointment = await AddAppointmentAsync(db, ct, startsAt: withPositiveOffset);

        await using var reading = _fixture.CreateContext();

        var stored = await reading.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);

        stored.StartsAtUtc.Should().Be(Noon);
    }

    [Fact]
    public async Task SaveChanges_ABayAndTechnicianFromDifferentDealerships_IsRejected()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var first = await AddDealershipAsync(db, ct);
        var second = await AddDealershipAsync(db, ct);

        // The bay belongs to the first dealership and the technician to the second, while the
        // appointment claims the first. FK_Appointments_Technician makes that unrepresentable.
        var bay = ServiceBay.Create(Guid.NewGuid(), first.Id, NewCode("B"));
        var technician = Technician.Create(Guid.NewGuid(), second.Id, "Cross Dealership");
        var serviceType = await AddServiceTypeAsync(db, ct);
        var (customer, vehicle) = await AddCustomerWithVehicleAsync(db, ct);

        db.ServiceBays.Add(bay);
        db.Technicians.Add(technician);
        await db.SaveChangesAsync(ct);

        db.Appointments.Add(Appointment.Book(
            Guid.NewGuid(), first.Id, bay.Id, technician.Id, serviceType,
            vehicle.Id, customer.Id, Noon, Noon));

        var save = async () => await db.SaveChangesAsync(ct);

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_ModifyingAVersionedEntity_IncrementsItsVersion()
    {
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var appointment = await AddAppointmentAsync(db, ct);
        appointment.Version.Should().Be(0, "a newly inserted row starts at the schema default");

        appointment.Start();
        await new UnitOfWork(db).SaveChangesAsync(ct);

        appointment.Version.Should().Be(1);

        await using var reading = _fixture.CreateContext();
        var stored = await reading.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);

        stored.Version.Should().Be(1);
        stored.Status.Should().Be(AppointmentStatus.InProgress);
    }

    [Fact]
    public async Task SaveChangesAsync_CalledOnTheContextDirectlyBypassingUnitOfWork_StillIncrementsVersion()
    {
        // The scenario plan 2b names explicitly: a repository (or, here, a test double for one)
        // reaches AppointmentDbContext.SaveChangesAsync directly rather than going through
        // IUnitOfWork. The increment lives on the context override precisely so this path cannot
        // silently disable the concurrency guard.
        await using var db = _fixture.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var appointment = await AddAppointmentAsync(db, ct);

        appointment.Start();
        await db.SaveChangesAsync(ct);

        appointment.Version.Should().Be(1, "the context's own override bumps the version, with no UnitOfWork involved");
    }

    [Fact]
    public async Task SaveChangesAsync_AWriteThatLostTheRace_ThrowsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var seeding = _fixture.CreateContext();
        var appointment = await AddAppointmentAsync(seeding, ct);

        // Two contexts load the same row, so both hold Version 0 as their original value.
        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();

        var fromFirst = await first.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);
        var fromSecond = await second.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);

        fromFirst.Start();
        await new UnitOfWork(first).SaveChangesAsync(ct);

        // The row is now at Version 1, so the second UPDATE's WHERE Version = 0 matches no row.
        fromSecond.Cancel();

        var save = async () => await new UnitOfWork(second).SaveChangesAsync(ct);

        await save.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    private static string NewCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<Dealership> AddDealershipAsync(
        AppointmentDbContext db, CancellationToken ct)
    {
        var dealership = Dealership.Create(
            Guid.NewGuid(), "Test Motors", "Asia/Ho_Chi_Minh",
            [OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))]);

        db.Dealerships.Add(dealership);
        await db.SaveChangesAsync(ct);

        return dealership;
    }

    private static async Task<Skill> AddSkillAsync(AppointmentDbContext db, CancellationToken ct)
    {
        var skill = Skill.Create(Guid.NewGuid(), NewCode("SKILL"), "A skill");

        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);

        return skill;
    }

    private static async Task<ServiceType> AddServiceTypeAsync(
        AppointmentDbContext db, CancellationToken ct)
    {
        var serviceType = ServiceType.Create(
            Guid.NewGuid(), NewCode("SVC"), "An hour of work", 60);

        db.ServiceTypes.Add(serviceType);
        await db.SaveChangesAsync(ct);

        return serviceType;
    }

    private static async Task<(Customer Customer, Vehicle Vehicle)> AddCustomerWithVehicleAsync(
        AppointmentDbContext db, CancellationToken ct)
    {
        var customer = Customer.Create(Guid.NewGuid(), "Test Owner");
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            customer.Id,
            $"{Guid.NewGuid():N}".ToUpperInvariant()[..17],
            "Toyota",
            "Corolla");

        db.Customers.Add(customer);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        return (customer, vehicle);
    }

    private static async Task<Appointment> AddAppointmentAsync(
        AppointmentDbContext db, CancellationToken ct, DateTimeOffset? startsAt = null)
    {
        var dealership = await AddDealershipAsync(db, ct);
        var serviceType = await AddServiceTypeAsync(db, ct);
        var (customer, vehicle) = await AddCustomerWithVehicleAsync(db, ct);

        var bay = ServiceBay.Create(Guid.NewGuid(), dealership.Id, NewCode("B"));
        var technician = Technician.Create(Guid.NewGuid(), dealership.Id, "Test Technician");

        db.ServiceBays.Add(bay);
        db.Technicians.Add(technician);
        await db.SaveChangesAsync(ct);

        var appointment = Appointment.Book(
            Guid.NewGuid(), dealership.Id, bay.Id, technician.Id, serviceType,
            vehicle.Id, customer.Id, startsAt ?? Noon, Noon);

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);

        return appointment;
    }
}
