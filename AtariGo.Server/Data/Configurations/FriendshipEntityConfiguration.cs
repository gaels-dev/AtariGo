using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class FriendshipEntityConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> entity)
    {
        entity.HasKey(friendship => new { friendship.UserId, friendship.FriendUserId });

        entity.HasIndex(friendship => new { friendship.FriendUserId, friendship.UserId },
            "IX_Friendships_FriendUserId");

        entity.Property(friendship => friendship.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Friendships_CreatedAt");

        entity.HasOne(dependent => dependent.FriendUser)
            .WithMany(principal => principal.FriendshipFriendUsers)
            .HasForeignKey(dependent => dependent.FriendUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Friendships_Friend");

        entity.HasOne(dependent => dependent.User).WithMany(principal => principal.FriendshipUsers)
            .HasForeignKey(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Friendships_User");
    }
}
