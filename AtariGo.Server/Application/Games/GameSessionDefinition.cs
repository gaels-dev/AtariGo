namespace AtariGo.Server.Application.Games;

public sealed record GameSessionDefinition
{
    public GameRuntimeState? Progress { get; init; }

    public long Version { get; init; }

    public long GameId { get; init; }

    public int CaptureGoal { get; init; }

    public int GameTimeSeconds { get; init; }

    public int TurnTimeSeconds { get; init; }

    public GameSessionParticipant Player { get; init; } = null!;

    public GameSessionParticipant Opponent { get; init; } = null!;
}
