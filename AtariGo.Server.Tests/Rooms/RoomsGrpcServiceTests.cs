using System.Net;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Contracts;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Services;

namespace AtariGo.Server.Tests.Rooms;

[TestClass]
[TestCategory("Integration")]
public sealed class RoomsGrpcServiceTests
{
    private const int DynamicPort = 0;
    private const int TestTimeoutSeconds = 10;

    [TestMethod]
    public async Task WatchRoom_TwoClients_ObserveSameParticipantsAndClosure()
    {
        // Arrange
        await using WebApplication app = await StartServerAsync();
        string address = app.Urls.Single();
        using GrpcChannel hostChannel = GrpcChannel.ForAddress(address);
        using GrpcChannel guestChannel = GrpcChannel.ForAddress(address);
        var host = new Contracts.Rooms.RoomsClient(hostChannel);
        var guest = new Contracts.Rooms.RoomsClient(guestChannel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TestTimeoutSeconds));
        CallOptions hostOptions = CreateOptions("host", timeout.Token);
        CallOptions guestOptions = CreateOptions("guest", timeout.Token);
        CreatePrivateRoomResponse created = await host.CreatePrivateRoomAsync(
            CreateRoomRequest(), hostOptions);
        var watchRequest = new WatchRoomRequest { RoomId = created.Room.RoomId };
        using AsyncServerStreamingCall<RoomSnapshot> hostWatch =
            host.WatchRoom(watchRequest, hostOptions);
        bool receivedInitial = await hostWatch.ResponseStream.MoveNext(timeout.Token);

        // Act
        CreatePrivateRoomResponse retried = await host.CreatePrivateRoomAsync(
            CreateRoomRequest(), hostOptions);
        JoinRoomResponse joined = await guest.JoinRoomAsync(new JoinRoomRequest
        {
            InvitationCode = created.Room.InvitationCode
        }, guestOptions);
        using AsyncServerStreamingCall<RoomSnapshot> guestWatch =
            guest.WatchRoom(watchRequest, guestOptions);
        bool receivedHostUpdate = await hostWatch.ResponseStream.MoveNext(timeout.Token);
        bool receivedGuestSnapshot = await guestWatch.ResponseStream.MoveNext(timeout.Token);
        RoomSnapshot hostSnapshot = hostWatch.ResponseStream.Current;
        RoomSnapshot guestSnapshot = guestWatch.ResponseStream.Current;
        await host.LeaveRoomAsync(
            new LeaveRoomRequest { RoomId = joined.Room.RoomId }, hostOptions);
        bool receivedClosure = await guestWatch.ResponseStream.MoveNext(timeout.Token);
        RoomSnapshot closed = guestWatch.ResponseStream.Current;

        // Assert
        Assert.IsTrue(receivedInitial);
        Assert.IsTrue(receivedHostUpdate);
        Assert.IsTrue(receivedGuestSnapshot);
        Assert.AreEqual(created.Room, retried.Room);
        Assert.AreEqual(created.Room.RoomId, joined.Room.RoomId);
        Assert.AreEqual(joined.Room, hostSnapshot);
        Assert.AreEqual(hostSnapshot, guestSnapshot);
        Assert.HasCount(2, guestSnapshot.Participants);
        Assert.IsTrue(receivedClosure);
        Assert.AreEqual(RoomState.Closed, closed.State);
        Assert.AreEqual(guestSnapshot.Version + 1, closed.Version);
        Assert.IsFalse(await guestWatch.ResponseStream.MoveNext(timeout.Token));
    }

    [TestMethod]
    public async Task JoinRoom_ConcurrentRequests_AssignsExactlyOneOpponent()
    {
        // Arrange
        await using WebApplication app = await StartServerAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.Rooms.RoomsClient(channel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TestTimeoutSeconds));
        CreatePrivateRoomResponse created = await client.CreatePrivateRoomAsync(
            CreateRoomRequest(), CreateOptions("host", timeout.Token));
        var request = new JoinRoomRequest { InvitationCode = created.Room.InvitationCode };

        // Act
        Task<StatusCode> guest = JoinAndGetStatusAsync(
            client, request, CreateOptions("guest", timeout.Token));
        Task<StatusCode> other = JoinAndGetStatusAsync(
            client, request, CreateOptions("other", timeout.Token));
        StatusCode[] results = await Task.WhenAll(guest, other);

        // Assert
        CollectionAssert.AreEquivalent(
            new[] { StatusCode.OK, StatusCode.ResourceExhausted }, results);
    }

    [TestMethod]
    public async Task JoinRoom_InvalidAccess_RejectsUnknownCodeUnauthenticatedCallerAndGuessedId()
    {
        // Arrange
        await using WebApplication app = await StartServerAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.Rooms.RoomsClient(channel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TestTimeoutSeconds));
        CreatePrivateRoomResponse created = await client.CreatePrivateRoomAsync(
            CreateRoomRequest(), CreateOptions("host", timeout.Token));
        CallOptions guestOptions = CreateOptions("guest", timeout.Token);

        // Act
        StatusCode unknownCode = await JoinAndGetStatusAsync(
            client, new JoinRoomRequest { InvitationCode = "invalid" }, guestOptions);
        StatusCode guessedId = await JoinAndGetStatusAsync(
            client, new JoinRoomRequest { RoomId = created.Room.RoomId }, guestOptions);
        StatusCode unauthenticated = await JoinAndGetStatusAsync(
            client, new JoinRoomRequest { InvitationCode = created.Room.InvitationCode },
            CreateOptions("unknown", timeout.Token));

        // Assert
        Assert.AreEqual(StatusCode.NotFound, unknownCode);
        Assert.AreEqual(StatusCode.PermissionDenied, guessedId);
        Assert.AreEqual(StatusCode.Unauthenticated, unauthenticated);
    }

    private static async Task<WebApplication> StartServerAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(ConfigureListener);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IRoomsService, RoomsService>();
        builder.Services.AddSingleton<ISessionService, TestRoomSessionService>();

        WebApplication app = builder.Build();
        app.MapGrpcService<RoomsGrpcService>();
        await app.StartAsync();
        return app;
    }

    private static void ConfigureListener(KestrelServerOptions options)
    {
        options.Listen(IPAddress.Loopback, DynamicPort, listener =>
            listener.Protocols = HttpProtocols.Http2);
    }

    private static CreatePrivateRoomRequest CreateRoomRequest()
    {
        return new CreatePrivateRoomRequest
        {
            Configuration = new RoomConfiguration
            {
                CaptureGoal = 1,
                GameTimeSeconds = 600,
                TurnTimeSeconds = 30
            }
        };
    }

    private static CallOptions CreateOptions(string token, CancellationToken cancellationToken)
    {
        return new CallOptions(
            headers: new Metadata { { "authorization", $"Bearer {token}" } },
            cancellationToken: cancellationToken);
    }

    private static async Task<StatusCode> JoinAndGetStatusAsync(
        Contracts.Rooms.RoomsClient client,
        JoinRoomRequest request,
        CallOptions options)
    {
        StatusCode status = StatusCode.OK;
        try
        {
            await client.JoinRoomAsync(request, options);
        }
        catch (RpcException exception)
        {
            status = exception.StatusCode;
        }

        return status;
    }
}
