namespace AtariGo.Server.Application.Games;

public sealed record GameOperationResult
{
    public long GameId { get; init; }

    public GameRuntimeState? Progress { get; init; }

    public long Version { get; init; }

    public GameActionResult? ActionResult { get; init; }
}
