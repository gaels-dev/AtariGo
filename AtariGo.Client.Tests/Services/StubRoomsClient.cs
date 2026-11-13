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

    public UpdateRoomConfigurationRequest? UpdateRequest { get; private set; }

    public Func<UpdateRoomConfigurationRequest, Task<RoomSnapshot>>? UpdateHandler { get; set; }

    public Func<WatchRoomRequest, CancellationToken, IAsyncEnumerable<RoomSnapshot>>?
        WatchHandler { get; set; }

    public Func<StartGameRequest, Task>? StartHandler { get; set; }

    public List<string> Operations { get; } = new List<string>();

    public string? JoinedCode { get; private set; }

    public int LeftRoomId { get; private set; }

    public RoomConfiguration? CreatedConfiguration { get; private set; }

    public StartGameRequest? StartRequest { get; private set; }

    public WatchRoomRequest? Observation { get; private set; }

    public List<WatchRoomRequest> Observations { get; } = new List<WatchRoomRequest>();

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

    public Task<RoomSnapshot> UpdateRoomConfigurationAsync(
        UpdateRoomConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        Operations.Add("Update");
        UpdateRequest = request;
        if (UpdateHandler is not null)
        {
            return UpdateHandler(request);
        }

        RoomSnapshot confirmed = Room.Clone();
        confirmed.Configuration = request.Configuration.Clone();
        confirmed.Version = request.ExpectedVersion + 1;
        return Task.FromResult(confirmed);
    }

    public Task StartGameAsync(StartGameRequest request, CancellationToken cancellationToken)
    {
        Operations.Add("Start");
        StartRequest = request;
        return StartHandler is null ? Task.CompletedTask : StartHandler(request);
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
        Observations.Add(request);
        return WatchHandler is null
            ? Updates.ToAsyncEnumerable() : WatchHandler(request, cancellationToken);
    }
}
