using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Application.Rooms;

internal sealed class RoomEntry
{
    private readonly ILogger _logger;

    public RoomEntry(PrivateRoom snapshot, DateTimeOffset expiresAt, ILogger logger)
    {
        _logger = logger;
        Snapshot = snapshot;
        ExpiresAt = expiresAt;
    }

    public PrivateRoom Snapshot
    {
        get; private set;
    }

    public Guid? StartReservationId
    {
        get; set;
    }

    public Guid? CompletedStartReservationId
    {
        get; set;
    }

    public bool IsStarting => StartReservationId is not null;

    public DateTimeOffset ExpiresAt
    {
        get;
    }

    public TaskCompletionSource<PrivateRoom> Changed
    {
        get; private set;
    } =
        CreateSignal();

    public void Publish(PrivateRoom snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        PrivateRoom previous = Snapshot;
        TaskCompletionSource<PrivateRoom> previousSignal = Changed;
        Snapshot = snapshot;
        Changed = CreateSignal();
        previousSignal.SetResult(snapshot);
        LobbyOperation change = GetChange(previous, snapshot);
        if (change != LobbyOperation.Unspecified)
        {
            Record(change, Actor(previous, snapshot, change));
        }
    }

    internal void Record(LobbyOperation operation, int? userId = null)
    {
        ServerLobbyDiagnostics.Record(_logger, operation, new DiagnosticEventDetails
        {
            Outcome = DiagnosticOutcome.Succeeded,
            Context = new DiagnosticContext
            {
                RoomId = Snapshot.RoomId,
                GameId = Snapshot.GameId,
                UserId = userId
            }
        });
    }

    private static int? Actor(PrivateRoom previous, PrivateRoom current, LobbyOperation operation)
    {
        return operation switch
        {
            LobbyOperation.RoomJoined => current.Opponent?.UserId,
            LobbyOperation.RoomLeft => previous.Opponent?.UserId,
            LobbyOperation.RoomConfigured => current.Host.UserId,
            _ => null
        };
    }

    private static LobbyOperation GetChange(PrivateRoom previous, PrivateRoom current)
    {
        if (current.IsClosed && !previous.IsClosed)
        {
            return LobbyOperation.RoomClosed;
        }
        if (current.GameId != previous.GameId)
        {
            return LobbyOperation.RoomStarted;
        }
        if (current.Configuration != previous.Configuration)
        {
            return LobbyOperation.RoomConfigured;
        }
        if (current.Opponent?.UserId != previous.Opponent?.UserId)
        {
            return current.Opponent is null ? LobbyOperation.RoomLeft : LobbyOperation.RoomJoined;
        }
        return LobbyOperation.Unspecified;
    }

    private static TaskCompletionSource<PrivateRoom> CreateSignal()
    {
        return new TaskCompletionSource<PrivateRoom>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
