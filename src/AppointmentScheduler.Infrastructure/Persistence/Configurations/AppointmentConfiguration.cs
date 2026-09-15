using AppointmentScheduler.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Appointments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.DealershipId).IsRequired();
        builder.Property(a => a.ServiceBayId).IsRequired();
        builder.Property(a => a.TechnicianId).IsRequired();
        builder.Property(a => a.ServiceTypeId).IsRequired();
        builder.Property(a => a.VehicleId).IsRequired();
        builder.Property(a => a.CustomerId).IsRequired();
        builder.Property(a => a.StartsAtUtc).IsRequired();
        builder.Property(a => a.EndsAtUtc).IsRequired();
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().IsRequired();
        builder.Property(a => a.Version).IsConcurrencyToken();
        builder.Ignore(a => a.Slot);
        builder.Ignore(a => a.Occupies);
        builder.Ignore(a => a.IsTerminal);
    }
}
