using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class GameConfigurationEntityConfiguration
    : IEntityTypeConfiguration<GameConfiguration>
{
    public void Configure(EntityTypeBuilder<GameConfiguration> entity)
    {
        entity.Property(gameConfiguration => gameConfiguration.CaptureGoal).HasDefaultValue(
            (byte)1, "DF_GameConfigurations_CaptureGoal");
    }
}
