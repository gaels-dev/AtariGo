using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubRoomsClient : IRoomsClient
{
    public RoomSnapshot Room { get; } = new RoomSnapshot
    {
        RoomId = 42,
        InvitationCode = "AABBCCDDEE",
        Version = 1,
        Configuration = new RoomConfiguration { CaptureGoal = 2 }
    };

    public string? JoinedCode { get; private set; }

    public int LeftRoomId { get; private set; }

    public RoomConfiguration? CreatedConfiguration { get; private set; }

    public WatchRoomRequest? Observation { get; private set; }

    public List<RoomSnapshot> Updates { get; } = new List<RoomSnapshot>();

    public Task<RoomSnapshot> CreatePrivateRoomAsync(
        RoomConfiguration configuration,
        CancellationToken cancellationToken)
    {
        CreatedConfiguration = configuration;
        return Task.FromResult(Room);
    }

    public Task<JoinRoomResponse> JoinRoomAsync(
        string invitationCode,
        CancellationToken cancellationToken)
    {
        JoinedCode = invitationCode;
        return Task.FromResult(new JoinRoomResponse
        {
            Room = Room,
            AssignedParticipant = new RoomParticipant { IsHost = false }
        });
    }

    public Task LeaveRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        LeftRoomId = roomId;
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<RoomSnapshot> WatchRoomAsync(
        WatchRoomRequest request,
        CancellationToken cancellationToken)
    {
        Observation = request;
        return Updates.ToAsyncEnumerable();
    }
}
