using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManager.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(250);
        builder.Property(s => s.PostalCode).HasMaxLength(10);
        builder.Property(s => s.City).HasMaxLength(100).IsRequired();

        builder.ToTable("Stores");
    }
}
