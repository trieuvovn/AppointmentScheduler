using AppointmentScheduler.Domain.Catalogue;
using AppointmentScheduler.Domain.Resources;
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

        builder.HasMany(s => s.RequiredSkills)
               .WithMany()
               .UsingEntity(
                   "ServiceTypeRequiredSkills",
                   right => right.HasOne(typeof(Skill)).WithMany().HasForeignKey("SkillId"),
                   left => left.HasOne(typeof(ServiceType)).WithMany().HasForeignKey("ServiceTypeId"));

        builder.Navigation(s => s.RequiredSkills)
               .HasField("_requiredSkills")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
