using AppointmentScheduler.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Dealerships");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name).IsRequired();
        builder.Property(d => d.TimeZoneId).IsRequired();
        builder.OwnsMany<OpeningHoursEntry>("_openingHours", hours =>
        {
            hours.ToTable("DealershipOpeningHours");
            hours.WithOwner().HasForeignKey("DealershipId");
            hours.HasKey("DealershipId", nameof(OpeningHoursEntry.DayOfWeek));
            hours.Property(h => h.DayOfWeek).HasColumnType("tinyint");
        });

        builder.Navigation("_openingHours").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
