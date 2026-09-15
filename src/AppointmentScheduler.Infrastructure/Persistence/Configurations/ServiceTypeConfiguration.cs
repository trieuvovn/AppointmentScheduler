using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServiceTypes");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Code).IsRequired();
        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.DurationMinutes).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();

        builder.OwnsMany<EntityReference>("_requiredSkillIds", skills =>
        {
            skills.ToTable("ServiceTypeRequiredSkills");
            skills.WithOwner().HasForeignKey("ServiceTypeId");
            skills.HasKey("ServiceTypeId", nameof(EntityReference.Id));
            skills.Property(s => s.Id).HasColumnName("SkillId");
        });

        builder.Navigation("_requiredSkillIds").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
