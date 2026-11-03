using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class SanctionEntityConfiguration : IEntityTypeConfiguration<Sanction>
{
    public void Configure(EntityTypeBuilder<Sanction> entity)
    {
        entity.HasIndex(sanction => new { sanction.AdministratorId, sanction.StartedAt },
            "IX_Sanctions_Administrator").IsDescending(false, true);

        entity.HasIndex(sanction => sanction.ReportId, "IX_Sanctions_Report").HasFilter(
            "([ReportId] IS NOT NULL)");

        entity.HasIndex(sanction => new { sanction.UserId, sanction.State, sanction.StartedAt },
            "IX_Sanctions_User_State").IsDescending(false, false, true);

        entity.Property(sanction => sanction.EndsAt).HasPrecision(0);
        entity.Property(sanction => sanction.Reason).HasMaxLength(1000);
        entity.Property(sanction => sanction.SanctionType)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(sanction => sanction.StartedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Sanctions_StartedAt");
        entity.Property(sanction => sanction.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Active", "DF_Sanctions_State");

        entity.HasOne(dependent => dependent.Administrator)
            .WithMany(principal => principal.SanctionAdministrators)
            .HasForeignKey(dependent => dependent.AdministratorId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Sanctions_Administrator");

        entity.HasOne(dependent => dependent.Report).WithMany(principal => principal.Sanctions)
            .HasForeignKey(dependent => dependent.ReportId)
            .HasConstraintName("FK_Sanctions_Report");

        entity.HasOne(dependent => dependent.User).WithMany(principal => principal.SanctionUsers)
            .HasForeignKey(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Sanctions_User");
    }
}
