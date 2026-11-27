using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;
using AtariGo.Contracts.Validation;

namespace AtariGo.Server.Data.Configurations;

public sealed class ReportEntityConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> entity)
    {
        entity.ToTable("Reports", table =>
        {
            table.HasCheckConstraint("CK_Reports_DifferentUsers",
                "[ReporterId] <> [ReportedUserId]");
            table.HasCheckConstraint("CK_Reports_Category",
                "[Category] IN ('Chat', 'Inactivity', 'Manipulation')");
        });

        entity.HasIndex(report => new { report.ReporterId, report.GameId },
            "UQ_Reports_Reporter_Game").IsUnique();
        entity.HasIndex(report => new
        {
            report.ReportedUserId, report.Category, report.CreatedAt, report.ReporterId
        }, "IX_Reports_Target_Category_Created").IsDescending(false, false, true, false);

        entity.Property(report => report.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Reports_CreatedAt");
        entity.Property(report => report.Category)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(report => report.Description).HasMaxLength(DescriptionRules.MaximumLength);

        entity.HasOne(report => report.Game).WithMany(game => game.Reports)
            .HasForeignKey(report => report.GameId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Reports_Game");

        entity.HasOne(report => report.ReportedUser)
            .WithMany(user => user.ReportReportedUsers)
            .HasForeignKey(report => report.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Reports_ReportedUser");

        entity.HasOne(report => report.Reporter)
            .WithMany(user => user.ReportReporters)
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Reports_Reporter");
    }
}
