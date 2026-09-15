using AppointmentScheduler.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServiceBays");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.DealershipId).IsRequired();
        builder.Property(b => b.Code).IsRequired();
        builder.Property(b => b.IsActive).IsRequired();
        builder.Property(b => b.Version).IsConcurrencyToken();
    }
}
