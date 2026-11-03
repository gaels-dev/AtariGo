using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class ReportEntityConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> entity)
    {
        entity.HasIndex(report => new { report.ReportedUserId, report.CreatedAt },
            "IX_Reports_ReportedUser").IsDescending(false, true);

        entity.HasIndex(report => report.ReviewerId, "IX_Reports_Reviewer").HasFilter(
            "([ReviewerId] IS NOT NULL)");

        entity.HasIndex(report => new { report.State, report.CreatedAt },
            "IX_Reports_State_Created");

        entity.Property(report => report.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Reports_CreatedAt");
        entity.Property(report => report.Description).HasMaxLength(2000);
        entity.Property(report => report.ReportReason)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(report => report.ReviewedAt).HasPrecision(0);
        entity.Property(report => report.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Pending", "DF_Reports_State");

        entity.HasOne(dependent => dependent.Game).WithMany(principal => principal.Reports)
            .HasForeignKey(dependent => dependent.GameId)
            .HasConstraintName("FK_Reports_Game");

        entity.HasOne(dependent => dependent.ReportedUser)
            .WithMany(principal => principal.ReportReportedUsers)
            .HasForeignKey(dependent => dependent.ReportedUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Reports_ReportedUser");

        entity.HasOne(dependent => dependent.Reporter)
            .WithMany(principal => principal.ReportReporters)
            .HasForeignKey(dependent => dependent.ReporterId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Reports_Reporter");

        entity.HasOne(dependent => dependent.Reviewer)
            .WithMany(principal => principal.ReportReviewers)
            .HasForeignKey(dependent => dependent.ReviewerId)
            .HasConstraintName("FK_Reports_Reviewer");
    }
}
