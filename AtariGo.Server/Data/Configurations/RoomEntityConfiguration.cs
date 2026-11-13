using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class RoomEntityConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> entity)
    {
        entity.HasIndex(room => new { room.CreatorId, room.State }, "IX_Rooms_Creator_State");

        entity.HasIndex(room => room.InvitationCode, "UQ_Rooms_InvitationCode").IsUnique();

        entity.Property(room => room.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Rooms_CreatedAt");
        entity.Property(room => room.ExpiresAt).HasPrecision(0);
        entity.Property(room => room.InvitationCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(room => room.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Waiting", "DF_Rooms_State");

        entity.HasOne(dependent => dependent.Creator).WithMany(principal => principal.Rooms)
            .HasForeignKey(dependent => dependent.CreatorId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Rooms_Creator");
    }
}
