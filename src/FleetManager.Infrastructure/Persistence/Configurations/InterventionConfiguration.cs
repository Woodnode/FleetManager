using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManager.Infrastructure.Persistence.Configurations;

public class InterventionConfiguration : IEntityTypeConfiguration<Intervention>
{
    public void Configure(EntityTypeBuilder<Intervention> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Type).IsRequired();
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.PlannedStartDate).IsRequired();
        builder.Property(i => i.PlannedEndDate).IsRequired();
        builder.Property(i => i.Comment).HasMaxLength(1000);

        builder.HasOne(i => i.Vehicle)
               .WithMany(v => v.Interventions)
               .HasForeignKey(i => i.VehicleId)
               .OnDelete(DeleteBehavior.Restrict);

        // Même filtre que Vehicle (suppression logique) : sans lui, les requêtes qui chargent le
        // véhicule (listes) excluaient ces interventions alors que les comptages les incluaient.
        builder.HasQueryFilter(i => !i.Vehicle.IsDeleted);

        builder.HasOne(i => i.Store)
               .WithMany()
               .HasForeignKey(i => i.StoreId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Technician)
               .WithMany()
               .HasForeignKey(i => i.TechnicianId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("Interventions");
    }
}
