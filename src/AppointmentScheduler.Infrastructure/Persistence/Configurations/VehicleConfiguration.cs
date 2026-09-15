using AppointmentScheduler.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.CustomerId).IsRequired();
        builder.Property(v => v.Vin).IsRequired();
        builder.Property(v => v.Make).IsRequired();
        builder.Property(v => v.Model).IsRequired();
    }
}
