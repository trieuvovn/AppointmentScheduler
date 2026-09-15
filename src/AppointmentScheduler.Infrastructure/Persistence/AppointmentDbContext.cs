using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Customers;
using AppointmentScheduler.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence;

public sealed class AppointmentDbContext : DbContext
{
    public AppointmentDbContext(DbContextOptions<AppointmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();

    public DbSet<Technician> Technicians => Set<Technician>();

    public DbSet<Dealership> Dealerships => Set<Dealership>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    /// <summary>
    /// Bumps the concurrency token of every modified versioned entity, then saves.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IVersioned>()
                              .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.Version++;
        }

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The appointment's resources were modified by another transaction.", ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppointmentDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Every instant in this model is UTC in a datetime2 column, so the convention is set once
        // here rather than repeated on each property.
        configurationBuilder.Properties<DateTimeOffset>()
                            .HaveConversion<UtcDateTimeOffsetConverter, UtcDateTimeOffsetComparer>();
    }
}
