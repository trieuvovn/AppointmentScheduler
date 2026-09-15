using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Technicians");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.DealershipId).IsRequired();
        builder.Property(t => t.FullName).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.Version).IsConcurrencyToken();
        builder.OwnsMany<EntityReference>("_skillIds", skills =>
        {
            skills.ToTable("TechnicianSkills");
            skills.WithOwner().HasForeignKey("TechnicianId");
            skills.HasKey("TechnicianId", nameof(EntityReference.Id));
            skills.Property(s => s.Id).HasColumnName("SkillId");
        });

        builder.Navigation("_skillIds").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
