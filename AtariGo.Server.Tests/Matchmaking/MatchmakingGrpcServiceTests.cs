using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Matchmaking;

[TestClass]
[TestCategory("Integration")]
public sealed class MatchmakingGrpcServiceTests
{
    private const int DynamicPort = 0;
    private const int TestTimeoutSeconds = 20;
    private const string GuestCredentialHeader = "x-guest-credential";

    [TestMethod]
    public async Task SearchPublicMatch_PlayerLeavesCountdown_RequeuesRemainingPlayerAsync()
    {
        // Arrange
        await using WebApplication app = await StartServerAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        AtariGo.Contracts.Matchmaking.MatchmakingClient client =
            new AtariGo.Contracts.Matchmaking.MatchmakingClient(channel);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(TestTimeoutSeconds));
        string leavingCredential = await CreateGuestCredentialAsync(
            client, "Guest-Leaving", timeout.Token);
        string remainingCredential = await CreateGuestCredentialAsync(
            client, "Guest-Remaining", timeout.Token);
        using CancellationTokenSource leavingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        using CancellationTokenSource remainingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        AsyncServerStreamingCall<MatchmakingEvent> leavingCall =
            StartSearch(client, leavingCredential, leavingCancellation.Token);
        using (leavingCall)
        {
            await ReadNextEventAsync(leavingCall, timeout.Token);
            AsyncServerStreamingCall<MatchmakingEvent> remainingCall =
                StartSearch(client, remainingCredential, remainingCancellation.Token);
            using (remainingCall)
            {
                await ReadNextEventAsync(leavingCall, timeout.Token);
                await ReadNextEventAsync(remainingCall, timeout.Token);
                await ReadNextEventAsync(remainingCall, timeout.Token);

                // Act
                leavingCancellation.Cancel();

                MatchmakingEvent cancelledEvent = await ReadNextEventAsync(
                    remainingCall, timeout.Token);
                MatchmakingEvent requeuedEvent = await ReadNextEventAsync(
                    remainingCall, timeout.Token);
                // Assert
                Assert.AreEqual(
                    MatchmakingEvent.EventOneofCase.MatchCountdownCancelled,
                    cancelledEvent.EventCase);
                Assert.AreEqual(
                    MatchmakingEvent.EventOneofCase.SearchQueued,
                    requeuedEvent.EventCase);

                await AssertRemainingPlayerPairsAgainAsync(
                    client,
                    remainingCall,
                    timeout.Token);
                remainingCancellation.Cancel();
            }
        }
    }

    [TestMethod]
    public async Task SearchPublicMatch_CountdownCompletes_ReturnsTheSameGameToBothPlayersAsync()
    {
        // Arrange
        ControlledPublicGameLifecycleService lifecycle = new ControlledPublicGameLifecycleService();
        lifecycle.Completion.SetResult(42);
        await using WebApplication app = await StartServerAsync(lifecycle,
            new AdvancingMatchmakingTimeProvider());
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        AtariGo.Contracts.Matchmaking.MatchmakingClient client =
            new AtariGo.Contracts.Matchmaking.MatchmakingClient(channel);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(TestTimeoutSeconds));
        string blackCredential = await CreateGuestCredentialAsync(client, "Guest-Black",
            timeout.Token);
        string whiteCredential = await CreateGuestCredentialAsync(client, "Guest-White",
            timeout.Token);
        using AsyncServerStreamingCall<MatchmakingEvent> black = StartSearch(client,
            blackCredential, timeout.Token);
        await ReadNextEventAsync(black, timeout.Token);
        using AsyncServerStreamingCall<MatchmakingEvent> white = StartSearch(client,
            whiteCredential, timeout.Token);
        await ReadNextEventAsync(black, timeout.Token);
        await ReadNextEventAsync(white, timeout.Token);
        await ReadNextEventAsync(white, timeout.Token);
        // Act
        MatchmakingEvent blackStarted = await ReadNextEventAsync(black, timeout.Token);
        MatchmakingEvent whiteStarted = await ReadNextEventAsync(white, timeout.Token);
        // Assert
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.PublicGameStarted, blackStarted.EventCase);
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.PublicGameStarted, whiteStarted.EventCase);
        Assert.AreEqual(42, blackStarted.PublicGameStarted.GameId);
        Assert.AreEqual(blackStarted.PublicGameStarted.GameId,
            whiteStarted.PublicGameStarted.GameId);
        Assert.AreEqual(1, lifecycle.CreationCount);
        Assert.IsFalse(await black.ResponseStream.MoveNext(timeout.Token));
        Assert.IsFalse(await white.ResponseStream.MoveNext(timeout.Token));
    }

    [TestMethod]
    public async Task SearchPublicMatch_AlreadySearching_ReturnsFailedPreconditionAsync()
    {
        // Arrange
        await using WebApplication app = await StartServerAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        AtariGo.Contracts.Matchmaking.MatchmakingClient client =
            new AtariGo.Contracts.Matchmaking.MatchmakingClient(channel);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(TestTimeoutSeconds));
        string credential = await CreateGuestCredentialAsync(client, "Guest-Same", timeout.Token);
        using AsyncServerStreamingCall<MatchmakingEvent> first = StartSearch(client,
            credential, timeout.Token);
        await ReadNextEventAsync(first, timeout.Token);
        using AsyncServerStreamingCall<MatchmakingEvent> second = StartSearch(client,
            credential, timeout.Token);
        // Act
        RpcException failure = await Assert.ThrowsAsync<RpcException>(() =>
            second.ResponseStream.MoveNext(timeout.Token));
        // Assert
        Assert.AreEqual(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    private static async Task AssertRemainingPlayerPairsAgainAsync(
        AtariGo.Contracts.Matchmaking.MatchmakingClient client,
        AsyncServerStreamingCall<MatchmakingEvent> remainingCall,
        CancellationToken cancellationToken)
    {
        string nextCredential = await CreateGuestCredentialAsync(
            client, "Guest-Next", cancellationToken);
        using CancellationTokenSource nextCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using AsyncServerStreamingCall<MatchmakingEvent> nextCall = StartSearch(
            client, nextCredential, nextCancellation.Token);

        await ReadNextEventAsync(nextCall, cancellationToken);
        MatchmakingEvent remainingCountdown = await ReadNextEventAsync(
            remainingCall, cancellationToken);
        MatchmakingEvent nextCountdown = await ReadNextEventAsync(
            nextCall, cancellationToken);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            remainingCountdown.EventCase);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            nextCountdown.EventCase);
        Assert.AreEqual(
            "Guest-Next",
            remainingCountdown.MatchCountdownStarted.OpponentName);
        nextCancellation.Cancel();
    }

    private static async Task<WebApplication> StartServerAsync(
        IGameLifecycleService? lifecycle = null, TimeProvider? timeProvider = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(ConfigureListener);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(timeProvider ?? TimeProvider.System);
        builder.Services.AddSingleton<IGuestIdentityRegistry, GuestIdentityRegistry>();
        builder.Services.AddSingleton<InMemoryPublicMatchmakingQueue>();
        builder.Services.AddScoped<PublicMatchmakingService>();
        builder.Services.AddSingleton<ISessionService, TestRoomSessionService>();
        builder.Services.AddSingleton<IRoomInvitationNotifier, SessionConnectionRegistry>();
        builder.Services.AddSingleton<IRoomsService, RoomsService>();
        builder.Services.AddSingleton<IGameLifecycleService>(provider => lifecycle
            ?? new TestGameLifecycleService(provider.GetRequiredService<IRoomsService>()));

        WebApplication app = builder.Build();
        app.MapGrpcService<MatchmakingGrpcService>();
        await app.StartAsync();
        return app;
    }

    private static void ConfigureListener(KestrelServerOptions options)
    {
        options.Listen(
            IPAddress.Loopback,
            DynamicPort,
            ConfigureHttp2Listener);
    }

    private static void ConfigureHttp2Listener(ListenOptions options)
    {
        options.Protocols = HttpProtocols.Http2;
    }

    private static async Task<string> CreateGuestCredentialAsync(
        AtariGo.Contracts.Matchmaking.MatchmakingClient client,
        string displayName,
        CancellationToken cancellationToken)
    {
        CreateGuestIdentityResponse response = await client.CreateGuestIdentityAsync(
            new CreateGuestIdentityRequest { DisplayName = displayName },
            new CallOptions(cancellationToken: cancellationToken));
        return response.Credential;
    }

    private static AsyncServerStreamingCall<MatchmakingEvent> StartSearch(
        AtariGo.Contracts.Matchmaking.MatchmakingClient client,
        string credential,
        CancellationToken cancellationToken)
    {
        Metadata headers = new Metadata
        {
            { GuestCredentialHeader, credential }
        };
        CallOptions options = new CallOptions(
            headers: headers,
            cancellationToken: cancellationToken);
        return client.SearchPublicMatch(new SearchPublicMatchRequest(), options);
    }

    private static async Task<MatchmakingEvent> ReadNextEventAsync(
        AsyncServerStreamingCall<MatchmakingEvent> call,
        CancellationToken cancellationToken)
    {
        bool hasEvent = await call.ResponseStream.MoveNext(cancellationToken);
        if (!hasEvent)
        {
            throw new InvalidOperationException("The matchmaking stream ended unexpectedly.");
        }

        return call.ResponseStream.Current;
    }
}
