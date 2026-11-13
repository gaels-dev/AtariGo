using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.HasIndex(role => role.Name, "UQ_Roles_Name").IsUnique();

        entity.Property(role => role.Description).HasMaxLength(500);
        entity.Property(role => role.Name).HasMaxLength(30);
    }
}
