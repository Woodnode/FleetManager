using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManager.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);

        // Empreinte SHA-256 en hexadécimal : 64 caractères.
        builder.Property(r => r.TokenHash)
               .HasMaxLength(64)
               .IsRequired();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.FamilyId);

        builder.Property(r => r.ExpiresAt).IsRequired();
        builder.Property(r => r.IsRevoked).IsRequired().HasDefaultValue(false);

        builder.HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("RefreshTokens");
    }
}
