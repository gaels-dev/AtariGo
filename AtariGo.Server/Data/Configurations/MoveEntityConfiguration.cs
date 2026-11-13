using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class MoveEntityConfiguration : IEntityTypeConfiguration<Move>
{
    public void Configure(EntityTypeBuilder<Move> entity)
    {
        entity.HasIndex(move => new { move.GamePlayerId, move.PlayedAt, move.MoveId },
            "IX_Moves_GamePlayer_PlayedAt");

        entity.HasIndex(move => new { move.GamePlayerId, move.TurnNumber },
            "UQ_Moves_Player_Turn").IsUnique();

        entity.Property(move => move.PlayedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_Moves_PlayedAt");

        entity.HasOne(dependent => dependent.GamePlayer).WithMany(principal => principal.Moves)
            .HasForeignKey(dependent => dependent.GamePlayerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Moves_GamePlayer");
    }
}
