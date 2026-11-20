using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class SessionEntityConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> entity)
    {
        entity.HasIndex(session => new { session.UserId, session.State, session.ExpiresAt },
            "IX_Sessions_User_State_Expiry");

        entity.HasIndex(session => session.SessionTokenHash, "UQ_Sessions_TokenHash").IsUnique();

        entity.Property(session => session.ExpiresAt).HasPrecision(0);
        entity.Property(session => session.LastActivityAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Sessions_LastActivityAt");
        entity.Property(session => session.SessionTokenHash)
            .HasMaxLength(255)
            .IsUnicode(false);
        entity.Property(session => session.StartedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Sessions_StartedAt");
        entity.Property(session => session.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Active", "DF_Sessions_State");

        entity.HasOne(dependent => dependent.User).WithMany(principal => principal.Sessions)
            .HasForeignKey(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Sessions_User");
    }
}
