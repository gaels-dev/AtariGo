using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class LoginChallengeEntityConfiguration : IEntityTypeConfiguration<LoginChallenge>
{
    public void Configure(EntityTypeBuilder<LoginChallenge> entity)
    {
        entity.HasKey(challenge => challenge.LoginChallengeId);
        entity.HasIndex(challenge => challenge.TokenHash).IsUnique();
        entity.HasIndex(challenge => new { challenge.UserId, challenge.ClosedAt });
        entity.Property(challenge => challenge.TokenHash).HasMaxLength(64).IsUnicode(false)
            .UseCollation("Latin1_General_100_BIN2");
        entity.Property(challenge => challenge.CodeBinding).HasMaxLength(64).IsUnicode(false);
        entity.Property(challenge => challenge.DeliveryState).HasMaxLength(20).IsUnicode(false);
        entity.Property(challenge => challenge.CreatedAt).HasPrecision(0);
        entity.Property(challenge => challenge.IssueUntil).HasPrecision(0);
        entity.Property(challenge => challenge.ClosedAt).HasPrecision(0);
        entity.Property(challenge => challenge.LastSentAt).HasPrecision(0);
        entity.HasOne(challenge => challenge.User).WithMany()
            .HasForeignKey(challenge => challenge.UserId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne(challenge => challenge.ActiveCode).WithMany()
            .HasForeignKey(challenge => challenge.ActiveCodeId).OnDelete(DeleteBehavior.NoAction);
    }
}
