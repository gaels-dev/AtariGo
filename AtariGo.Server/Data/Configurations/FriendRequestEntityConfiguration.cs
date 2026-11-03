using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class FriendRequestEntityConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> entity)
    {
        entity.HasIndex(friendRequest => new
        {
            friendRequest.RecipientId,
            friendRequest.State,
            friendRequest.RequestedAt
        }, "IX_FriendRequests_Recipient_State").IsDescending(
                false, false, true);

        entity.HasIndex(friendRequest => new
        {
            friendRequest.RequesterId,
            friendRequest.RequestedAt
        }, "IX_FriendRequests_Requester").IsDescending(false, true);

        entity.HasIndex(friendRequest => new
        {
            friendRequest.RequesterId,
            friendRequest.RecipientId
        }, "UX_FriendRequests_PendingPair")
            .IsUnique()
            .HasFilter("([State]='Pending')");

        entity.Property(friendRequest => friendRequest.RequestedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_FriendRequests_RequestedAt");
        entity.Property(friendRequest => friendRequest.RespondedAt).HasPrecision(0);
        entity.Property(friendRequest => friendRequest.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Pending", "DF_FriendRequests_State");

        entity.HasOne(dependent => dependent.Recipient)
            .WithMany(principal => principal.FriendRequestRecipients)
            .HasForeignKey(dependent => dependent.RecipientId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_FriendRequests_Recipient");

        entity.HasOne(dependent => dependent.Requester)
            .WithMany(principal => principal.FriendRequestRequesters)
            .HasForeignKey(dependent => dependent.RequesterId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_FriendRequests_Requester");
    }
}
