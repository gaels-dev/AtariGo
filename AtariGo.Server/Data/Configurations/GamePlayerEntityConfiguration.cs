using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class GamePlayerEntityConfiguration : IEntityTypeConfiguration<GamePlayer>
{
    public void Configure(EntityTypeBuilder<GamePlayer> entity)
    {
        entity.HasIndex(gamePlayer => gamePlayer.UserId, "IX_GamePlayers_UserId");

        entity.HasIndex(gamePlayer => new { gamePlayer.GameId, gamePlayer.StoneColor },
            "UQ_GamePlayers_Game_Color").IsUnique();

        entity.HasIndex(gamePlayer => new { gamePlayer.GameId, gamePlayer.GamePlayerId },
            "UQ_GamePlayers_Game_Player").IsUnique();

        entity.HasIndex(gamePlayer => new { gamePlayer.GameId, gamePlayer.TurnOrder },
            "UQ_GamePlayers_Game_TurnOrder").IsUnique();

        entity.HasIndex(gamePlayer => new { gamePlayer.GameId, gamePlayer.UserId },
            "UX_GamePlayers_Game_User")
            .IsUnique()
            .HasFilter("([UserId] IS NOT NULL)");

        entity.HasIndex(gamePlayer => new
            {
                gamePlayer.GameId,
                gamePlayer.GuestIdentityId
            }, "UX_GamePlayers_Game_GuestIdentity")
            .IsUnique()
            .HasFilter("([GuestIdentityId] IS NOT NULL)");

        entity.Property(gamePlayer => gamePlayer.GuestName).HasMaxLength(100);
        entity.Property(gamePlayer => gamePlayer.GuestIdentityId)
            .HasColumnType("uniqueidentifier");
        entity.Property(gamePlayer => gamePlayer.JoinedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_GamePlayers_JoinedAt");
        entity.Property(gamePlayer => gamePlayer.LeftAt).HasPrecision(0);
        entity.Property(gamePlayer => gamePlayer.Result)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Pending", "DF_GamePlayers_Result");
        entity.Property(gamePlayer => gamePlayer.StoneColor)
            .HasMaxLength(10)
            .IsUnicode(false);

        entity.HasOne(dependent => dependent.Game).WithMany(principal => principal.GamePlayers)
            .HasForeignKey(dependent => dependent.GameId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_GamePlayers_Game");

        entity.HasOne(dependent => dependent.User).WithMany(principal => principal.GamePlayers)
            .HasForeignKey(dependent => dependent.UserId)
            .HasConstraintName("FK_GamePlayers_User");
    }
}
