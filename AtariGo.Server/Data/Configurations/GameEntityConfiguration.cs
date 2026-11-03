using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class GameEntityConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> entity)
    {
        entity.HasIndex(game => game.GameConfigurationId, "IX_Games_Configuration");

        entity.HasIndex(game => new { game.State, game.GameType }, "IX_Games_State_Type");

        entity.HasIndex(game => game.RoomId, "UX_Games_RoomId")
            .IsUnique()
            .HasFilter("([RoomId] IS NOT NULL)");

        entity.Property(game => game.GameId).ValueGeneratedOnAdd();
        entity.Property(game => game.EndReason)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(game => game.FinishedAt).HasPrecision(0);
        entity.Property(game => game.GameType)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(game => game.StartedAt).HasPrecision(0);
        entity.Property(game => game.State)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Waiting", "DF_Games_State");

        entity.HasOne(dependent => dependent.GameConfiguration)
            .WithMany(principal => principal.Games)
            .HasForeignKey(dependent => dependent.GameConfigurationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Games_Configuration");

        entity.HasOne(dependent => dependent.Room).WithOne(principal => principal.Game)
            .HasForeignKey<Game>(dependent => dependent.RoomId)
            .HasConstraintName("FK_Games_Room");

        entity.HasOne(dependent => dependent.GamePlayer).WithMany(principal => principal.Games)
            .HasPrincipalKey(principal => new { principal.GameId, principal.GamePlayerId })
            .HasForeignKey(dependent => new { dependent.GameId, dependent.CurrentTurnGamePlayerId })
            .HasConstraintName("FK_Games_CurrentTurnPlayer");
    }
}
