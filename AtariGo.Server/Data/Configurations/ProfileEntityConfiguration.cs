using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class ProfileEntityConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> entity)
    {
        entity.HasKey(profile => profile.UserId);

        entity.Property(profile => profile.UserId).ValueGeneratedNever();
        entity.Property(profile => profile.Description).HasMaxLength(500);
        entity.Property(profile => profile.LastUpdate).HasPrecision(0);

        entity.HasOne(dependent => dependent.User).WithOne(principal => principal.Profile)
            .HasForeignKey<Profile>(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Profiles_User");
    }
}
