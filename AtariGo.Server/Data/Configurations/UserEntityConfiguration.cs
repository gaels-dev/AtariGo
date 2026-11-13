using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users", table => table.HasCheckConstraint(
            "CK_Users_UserName_NotGuest",
            "LOWER(LTRIM(RTRIM([UserName]))) <> N'guest' "
                + "AND LEFT(LOWER(LTRIM(RTRIM([UserName]))), 6) "
                + "<> N'guest-'"));

        entity.HasIndex(user => user.RoleId, "IX_Users_RoleId");

        entity.HasIndex(user => user.Email, "UQ_Users_Email").IsUnique();

        entity.HasIndex(user => user.UserName, "UQ_Users_UserName").IsUnique();

        entity.Property(user => user.Email).HasMaxLength(254);
        entity.Property(user => user.LastUpdate).HasPrecision(0);
        entity.Property(user => user.RegistrationDate)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Users_RegistrationDate");
        entity.Property(user => user.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Active", "DF_Users_State");
        entity.Property(user => user.UserName).HasMaxLength(15);

        entity.HasOne(dependent => dependent.Role).WithMany(principal => principal.Users)
            .HasForeignKey(dependent => dependent.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Users_Role");
    }
}
