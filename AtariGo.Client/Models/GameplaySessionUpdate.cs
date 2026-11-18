using AtariGo.Contracts;

namespace AtariGo.Client.Models;

public sealed record GameplaySessionUpdate(
    GameplayConnectionState State,
    ClientFailureReason Failure,
    bool HasPendingAction)
{
    public GameSessionServerEvent? ServerEvent
    {
        get; init;
    }

    public bool IsMatchingActionResult
    {
        get; init;
    }

    public bool IsSurrenderResult
    {
        get; init;
    }

    public bool ChatSendFailed
    {
        get; init;
    }

    public bool InvalidSnapshot
    {
        get; init;
    }

    public bool ConnectionStarted
    {
        get; init;
    }
}
