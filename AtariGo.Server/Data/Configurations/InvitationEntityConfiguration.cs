using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class InvitationEntityConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> entity)
    {
        entity.HasIndex(invitation => new { invitation.RecipientId, invitation.State },
            "IX_Invitations_Recipient_State").HasFilter("([RecipientId] IS NOT NULL)");

        entity.HasIndex(invitation => new { invitation.RoomId, invitation.State },
            "IX_Invitations_Room_State");

        entity.HasIndex(invitation => new { invitation.SenderId, invitation.CreatedAt },
            "IX_Invitations_Sender_Created").IsDescending(false, true);

        entity.Property(invitation => invitation.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Invitations_CreatedAt");
        entity.Property(invitation => invitation.InvitationType)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(invitation => invitation.RecipientEmail).HasMaxLength(254);
        entity.Property(invitation => invitation.RespondedAt).HasPrecision(0);
        entity.Property(invitation => invitation.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Pending", "DF_Invitations_State");

        entity.HasOne(dependent => dependent.Recipient)
            .WithMany(principal => principal.InvitationRecipients)
            .HasForeignKey(dependent => dependent.RecipientId)
            .HasConstraintName("FK_Invitations_Recipient");

        entity.HasOne(dependent => dependent.Room).WithMany(principal => principal.Invitations)
            .HasForeignKey(dependent => dependent.RoomId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Invitations_Room");

        entity.HasOne(dependent => dependent.Sender)
            .WithMany(principal => principal.InvitationSenders)
            .HasForeignKey(dependent => dependent.SenderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Invitations_Sender");
    }
}
