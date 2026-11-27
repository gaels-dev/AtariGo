using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class CredentialEntityConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> entity)
    {
        entity.HasKey(credential => credential.UserId);

        entity.Property(credential => credential.UserId).ValueGeneratedNever();
        entity.Property(credential => credential.LockedUntil).HasPrecision(0);
        entity.Property(credential => credential.PasswordChangedAt).HasPrecision(0);
        entity.Property(credential => credential.PasswordHash)
            .HasMaxLength(255)
            .IsUnicode(false);

        entity.HasOne(dependent => dependent.User).WithOne(principal => principal.Credential)
            .HasForeignKey<Credential>(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Credentials_User");
    }
}
