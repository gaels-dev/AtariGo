using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class UserPreferenceEntityConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> entity)
    {
        entity.HasKey(userPreference => userPreference.UserId);

        entity.Property(userPreference => userPreference.UserId).ValueGeneratedNever();
        entity.Property(userPreference => userPreference.MusicEnabled).HasDefaultValue(true,
            "DF_UserPreferences_MusicEnabled");
        entity.Property(userPreference => userPreference.SoundEnabled).HasDefaultValue(true,
            "DF_UserPreferences_SoundEnabled");

        entity.HasOne(dependent => dependent.User).WithOne(principal => principal.UserPreference)
            .HasForeignKey<UserPreference>(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_UserPreferences_User");
    }
}
