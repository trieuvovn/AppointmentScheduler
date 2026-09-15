using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Customers;
using AppointmentScheduler.Domain.Resources;
using AppointmentScheduler.Infrastructure.Persistence;
using AppointmentScheduler.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Xunit;

namespace AppointmentScheduler.Api.IntegrationTests.Features.Availability;

[Collection(DatabaseCollection.Name)]
public class AvailabilityPredicateAgreementTests
{
    private static readonly DateTimeOffset ProbeStart = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSlot ProbeSlot = TimeSlot.Create(ProbeStart, ProbeStart.AddHours(1));

    private static readonly int[] Offsets = [-120, -60, -30, 0, 30, 60, 120];

    private readonly DatabaseFixture _fixture;

    public AvailabilityPredicateAgreementTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindFreeBaysAsync_SevenBaysEachWithOneAppointment_MatchesTheDomainOverlapRule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateContext();

        var dealership = await AddDealershipAsync(db, ct);
        var serviceType = await AddServiceTypeAsync(db, ct);
        var (customer, vehicle) = await AddCustomerWithVehicleAsync(db, ct);

        var bays = new List<(ServiceBay Bay, TimeSlot AppointmentSlot)>();

        foreach (var offsetMinutes in Offsets)
        {
            var bay = ServiceBay.Create(Guid.NewGuid(), dealership.Id, NewCode("B"));
            var technician = Technician.Create(Guid.NewGuid(), dealership.Id, "Occupant");
            db.ServiceBays.Add(bay);
            db.Technicians.Add(technician);
            await db.SaveChangesAsync(ct);

            var appointmentStart = ProbeStart.AddMinutes(offsetMinutes);
            db.Appointments.Add(Appointment.Book(
                Guid.NewGuid(), dealership.Id, bay.Id, technician.Id, serviceType,
                vehicle.Id, customer.Id, appointmentStart, appointmentStart));
            await db.SaveChangesAsync(ct);

            var appointmentSlot = TimeSlot.FromDuration(appointmentStart, serviceType.Duration);
            bays.Add((bay, appointmentSlot));
        }

        // An eighth bay with an overlapping but Cancelled appointment, which Occupying() must exclude.
        var cancelledBay = ServiceBay.Create(Guid.NewGuid(), dealership.Id, NewCode("B"));
        var cancelledTechnician = Technician.Create(Guid.NewGuid(), dealership.Id, "Cancelled Occupant");
        db.ServiceBays.Add(cancelledBay);
        db.Technicians.Add(cancelledTechnician);
        await db.SaveChangesAsync(ct);

        var cancelledAppointment = Appointment.Book(
            Guid.NewGuid(), dealership.Id, cancelledBay.Id, cancelledTechnician.Id, serviceType,
            vehicle.Id, customer.Id, ProbeStart, ProbeStart);
        cancelledAppointment.Cancel();
        db.Appointments.Add(cancelledAppointment);
        await db.SaveChangesAsync(ct);

        var repository = new AvailabilityRepository(db);
        var freeBays = await repository.FindFreeBaysAsync(dealership.Id, ProbeSlot, ct);
        var freeBayIds = freeBays.Select(b => b.Id).ToHashSet();

        foreach (var (bay, appointmentSlot) in bays)
        {
            var expectedFree = !appointmentSlot.Overlaps(ProbeSlot);

            freeBayIds.Contains(bay.Id).Should().Be(expectedFree,
                $"bay with appointment {appointmentSlot} against probe {ProbeSlot} should be free={expectedFree}");
        }

        freeBayIds.Should().Contain(cancelledBay.Id, "a cancelled appointment must not occupy its bay");
    }

    [Fact]
    public async Task FindFreeQualifiedTechniciansAsync_SevenTechniciansEachWithOneAppointment_MatchesTheDomainOverlapRule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateContext();

        var dealership = await AddDealershipAsync(db, ct);
        var skill = await AddSkillAsync(db, ct);
        var serviceType = await AddServiceTypeAsync(db, ct, requiredSkills: [skill]);
        var (customer, vehicle) = await AddCustomerWithVehicleAsync(db, ct);

        var technicians = new List<(Technician Technician, TimeSlot AppointmentSlot)>();

        foreach (var offsetMinutes in Offsets)
        {
            var bay = ServiceBay.Create(Guid.NewGuid(), dealership.Id, NewCode("B"));
            var technician = Technician.Create(Guid.NewGuid(), dealership.Id, "Occupant", skills: [skill]);
            db.ServiceBays.Add(bay);
            db.Technicians.Add(technician);
            await db.SaveChangesAsync(ct);

            var appointmentStart = ProbeStart.AddMinutes(offsetMinutes);
            db.Appointments.Add(Appointment.Book(
                Guid.NewGuid(), dealership.Id, bay.Id, technician.Id, serviceType,
                vehicle.Id, customer.Id, appointmentStart, appointmentStart));
            await db.SaveChangesAsync(ct);

            var appointmentSlot = TimeSlot.FromDuration(appointmentStart, serviceType.Duration);
            technicians.Add((technician, appointmentSlot));
        }

        // An unqualified technician, free the whole time, who must be absent regardless of overlap.
        var unqualifiedTechnician = Technician.Create(Guid.NewGuid(), dealership.Id, "Unqualified");
        db.Technicians.Add(unqualifiedTechnician);
        await db.SaveChangesAsync(ct);

        var repository = new AvailabilityRepository(db);
        var freeTechnicians = await repository.FindFreeQualifiedTechniciansAsync(
            dealership.Id, serviceType.Id, ProbeSlot, ct);
        var freeTechnicianIds = freeTechnicians.Select(t => t.Id).ToHashSet();

        foreach (var (technician, appointmentSlot) in technicians)
        {
            var expectedFree = !appointmentSlot.Overlaps(ProbeSlot);

            freeTechnicianIds.Contains(technician.Id).Should().Be(expectedFree,
                $"technician with appointment {appointmentSlot} against probe {ProbeSlot} should be free={expectedFree}");
        }

        freeTechnicianIds.Should().NotContain(unqualifiedTechnician.Id,
            "an unqualified technician must never be returned, regardless of overlap");
    }

    private static string NewCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<Dealership> AddDealershipAsync(AppointmentDbContext db, CancellationToken ct)
    {
        var dealership = Dealership.Create(
            Guid.NewGuid(), "Predicate Motors", "Europe/London",
            [Domain.Resources.OpeningHours.Create(DayOfWeek.Monday, new TimeOnly(0, 0), new TimeOnly(23, 59))]);

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
        AppointmentDbContext db, CancellationToken ct, IEnumerable<Skill>? requiredSkills = null)
    {
        var serviceType = ServiceType.Create(
            Guid.NewGuid(), NewCode("SVC"), "An hour of work", 60, requiredSkills: requiredSkills);

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
}
