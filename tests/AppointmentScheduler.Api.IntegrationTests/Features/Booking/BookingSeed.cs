using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Customers;
using AppointmentScheduler.Domain.Resources;
using AppointmentScheduler.Infrastructure.Persistence;

namespace AppointmentScheduler.Api.IntegrationTests.Features.Booking;

/// <summary>
/// A dealership, one bay, one technician, one customer and vehicle — the minimal set of reference
/// data a booking needs. Fresh GUIDs per call, following the existing private-static-builder pattern
/// (see <c>PersistenceTests</c>, <c>AvailabilityPredicateAgreementTests</c>), so tests never collide
/// with demo data or each other. Factored here for reuse by Stage 5.
/// </summary>
internal static class BookingSeed
{
    internal static string NewCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    internal static async Task<Dealership> AddDealershipAsync(
        AppointmentDbContext db,
        CancellationToken ct,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        string timeZoneId = "UTC")
    {
        var dealership = Dealership.Create(
            Guid.NewGuid(), "Booking Test Motors", timeZoneId,
            [OpeningHours.Create(dayOfWeek, new TimeOnly(8, 0), new TimeOnly(18, 0))]);

        db.Dealerships.Add(dealership);
        await db.SaveChangesAsync(ct);

        return dealership;
    }

    internal static async Task<ServiceType> AddServiceTypeAsync(
        AppointmentDbContext db, CancellationToken ct, int durationMinutes = 30)
    {
        var serviceType = ServiceType.Create(
            Guid.NewGuid(), NewCode("SVC"), "A service", durationMinutes);

        db.ServiceTypes.Add(serviceType);
        await db.SaveChangesAsync(ct);

        return serviceType;
    }

    internal static async Task<ServiceBay> AddBayAsync(
        AppointmentDbContext db, Guid dealershipId, CancellationToken ct)
    {
        var bay = ServiceBay.Create(Guid.NewGuid(), dealershipId, NewCode("B"));

        db.ServiceBays.Add(bay);
        await db.SaveChangesAsync(ct);

        return bay;
    }

    internal static async Task<Technician> AddTechnicianAsync(
        AppointmentDbContext db, Guid dealershipId, CancellationToken ct)
    {
        var technician = Technician.Create(Guid.NewGuid(), dealershipId, "Test Technician");

        db.Technicians.Add(technician);
        await db.SaveChangesAsync(ct);

        return technician;
    }

    internal static async Task<(Customer Customer, Vehicle Vehicle)> AddCustomerWithVehicleAsync(
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

    /// <summary>A full, ready-to-book scenario: dealership + bay + technician + service type + vehicle.</summary>
    internal static async Task<BookingScenario> BuildAsync(AppointmentDbContext db, CancellationToken ct)
    {
        var dealership = await AddDealershipAsync(db, ct);
        var serviceType = await AddServiceTypeAsync(db, ct);
        var bay = await AddBayAsync(db, dealership.Id, ct);
        var technician = await AddTechnicianAsync(db, dealership.Id, ct);
        var (customer, vehicle) = await AddCustomerWithVehicleAsync(db, ct);

        return new BookingScenario(dealership, serviceType, bay, technician, customer, vehicle);
    }
}

internal sealed record BookingScenario(
    Dealership Dealership,
    ServiceType ServiceType,
    ServiceBay Bay,
    Technician Technician,
    Customer Customer,
    Vehicle Vehicle);
