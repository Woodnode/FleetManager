using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManager.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(v => v.Id);

        builder.OwnsOne(v => v.Vin, vin =>
        {
            vin.Property(x => x.Value)
               .HasColumnName("Vin")
               .HasMaxLength(17)
               .IsRequired();

            vin.HasIndex(x => x.Value).IsUnique();
        });

        builder.Property(v => v.Brand).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Year).IsRequired();
        builder.Property(v => v.Mileage).IsRequired();
        builder.Property(v => v.Status).IsRequired();

        builder.Property(v => v.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(v => v.DeletedAt);

        builder.HasOne(v => v.Store)
               .WithMany(s => s.Vehicles)
               .HasForeignKey(v => v.StoreId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(v => !v.IsDeleted);

        builder.ToTable("Vehicles");
    }
}
