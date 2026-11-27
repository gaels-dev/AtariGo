using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class SanctionEntityConfiguration : IEntityTypeConfiguration<Sanction>
{
    public void Configure(EntityTypeBuilder<Sanction> entity)
    {
        entity.ToTable("Sanctions", table =>
        {
            table.HasCheckConstraint("CK_Sanctions_Type",
                "[SanctionType] IN ('ChatMute', 'TemporarySuspension')");
            table.HasCheckConstraint("CK_Sanctions_Threshold",
                "[Threshold] >= 3 AND [Threshold] % 3 = 0");
            table.HasCheckConstraint("CK_Sanctions_EndsAt", "[EndsAt] > [StartedAt]");
            table.HasCheckConstraint("CK_Sanctions_RevokedAt",
                "[RevokedAt] IS NULL OR [RevokedAt] >= [StartedAt]");
        });

        entity.HasIndex(sanction => new
        {
            sanction.UserId, sanction.SanctionType,
            sanction.StartedAt, sanction.EndsAt
        }, "IX_Sanctions_User_Dates").IsDescending(false, false, true, false);
        entity.HasIndex(sanction => sanction.TriggeringReportId,
            "UQ_Sanctions_TriggeringReport").IsUnique();

        entity.Property(sanction => sanction.EndsAt).HasPrecision(0);
        entity.Property(sanction => sanction.RevokedAt).HasPrecision(0);
        entity.Property(sanction => sanction.SanctionType)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(sanction => sanction.StartedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Sanctions_StartedAt");

        entity.HasOne(sanction => sanction.TriggeringReport)
            .WithOne(report => report.TriggeredSanction)
            .HasForeignKey<Sanction>(sanction => sanction.TriggeringReportId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Sanctions_TriggeringReport");

        entity.HasOne(sanction => sanction.User)
            .WithMany(user => user.SanctionUsers)
            .HasForeignKey(sanction => sanction.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Sanctions_User");
    }
}
