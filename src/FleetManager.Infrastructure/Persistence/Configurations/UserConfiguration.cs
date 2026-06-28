using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManager.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).IsRequired();

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(x => x.Value)
                 .HasColumnName("Email")
                 .HasMaxLength(254)
                 .IsRequired();

            email.HasIndex(x => x.Value).IsUnique();
        });

        builder.HasOne<Store>()
               .WithMany()
               .HasForeignKey(u => u.StoreId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);

        builder.ToTable("Users");
    }
}
