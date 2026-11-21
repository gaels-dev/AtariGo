using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

public sealed record GameRuntimeState
{
    public const string Waiting = "Waiting";
    public const string Playing = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const int ConnectionGraceSeconds = 120;
    public const int ReconnectionGraceMilliseconds = 30000;

    public GameState Board { get; init; } = new GameState();
    public string Status { get; init; } = Waiting;
    public DateTime DeadlineUtc
    {
        get; init;
    }
    public string EndReason { get; init; } = string.Empty;
    public long? PausedTurnRemainingMilliseconds { get; init; }
    public DateTime? BlackReconnectDeadlineUtc { get; init; }
    public DateTime? WhiteReconnectDeadlineUtc { get; init; }
    public long BlackGraceRemainingMilliseconds { get; init; } = ReconnectionGraceMilliseconds;
    public long WhiteGraceRemainingMilliseconds { get; init; } = ReconnectionGraceMilliseconds;
    public bool IsTerminal => Status == Completed || Status == Cancelled;
}
