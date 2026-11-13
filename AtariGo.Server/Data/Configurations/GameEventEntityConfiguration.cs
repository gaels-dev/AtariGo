using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class GameEventEntityConfiguration : IEntityTypeConfiguration<GameEvent>
{
    public void Configure(EntityTypeBuilder<GameEvent> entity)
    {
        entity.HasIndex(gameEvent => new
        {
            gameEvent.GameId,
            gameEvent.CreatedAt,
            gameEvent.GameEventId
        }, "IX_GameEvents_Game_Created");

        entity.HasIndex(gameEvent => new
        {
            gameEvent.GameId,
            gameEvent.GamePlayerId,
            gameEvent.ActionId
        }, "UX_GameEvents_Player_Action").IsUnique().HasFilter("[ActionId] IS NOT NULL");
        entity.Property(gameEvent => gameEvent.ActionId).HasMaxLength(64).IsUnicode(false);

        entity.Property(gameEvent => gameEvent.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_GameEvents_CreatedAt");
        entity.Property(gameEvent => gameEvent.Description).HasMaxLength(1000);
        entity.Property(gameEvent => gameEvent.EventType)
            .HasMaxLength(30)
            .IsUnicode(false);

        entity.HasOne(dependent => dependent.Game).WithMany(principal => principal.GameEvents)
            .HasForeignKey(dependent => dependent.GameId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_GameEvents_Game");

        entity.HasOne(dependent => dependent.GamePlayer).WithMany(principal => principal.GameEvents)
            .HasPrincipalKey(principal => new { principal.GameId, principal.GamePlayerId })
            .HasForeignKey(dependent => new { dependent.GameId, dependent.GamePlayerId })
            .HasConstraintName("FK_GameEvents_GamePlayer");
    }
}
