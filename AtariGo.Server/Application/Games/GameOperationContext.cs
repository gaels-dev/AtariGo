using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameOperationContext
{
    internal GameOperationContext(Game game, GameRuntimeState progress)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(progress);
        Game = game;
        Progress = progress;
    }

    public GameRuntimeState Progress
    {
        get;
    }

    public long Version => Game.StateVersion;

    internal Game Game
    {
        get;
    }
}
