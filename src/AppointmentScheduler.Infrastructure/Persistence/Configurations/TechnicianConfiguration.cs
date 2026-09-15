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

        builder.HasMany(t => t.Skills)
               .WithMany()
               .UsingEntity(
                   "TechnicianSkills",
                   right => right.HasOne(typeof(Skill)).WithMany().HasForeignKey("SkillId"),
                   left => left.HasOne(typeof(Technician)).WithMany().HasForeignKey("TechnicianId"));

        builder.Navigation(t => t.Skills)
               .HasField("_skills")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
