using System.Net;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    public async Task SearchPublicMatch_PlayerLeavesCountdown_RequeuesRemainingPlayer()
    {
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

                leavingCancellation.Cancel();

                MatchmakingEvent cancelledEvent = await ReadNextEventAsync(
                    remainingCall, timeout.Token);
                MatchmakingEvent requeuedEvent = await ReadNextEventAsync(
                    remainingCall, timeout.Token);
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

    private static async Task<WebApplication> StartServerAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(ConfigureListener);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IGuestIdentityRegistry, GuestIdentityRegistry>();
        builder.Services.AddSingleton<IPublicMatchmakingQueue, InMemoryPublicMatchmakingQueue>();
        builder.Services.AddSingleton<ISessionService, TestRoomSessionService>();
        builder.Services.AddSingleton<IRoomInvitationNotifier, SessionConnectionRegistry>();
        builder.Services.AddSingleton<IRoomsService, RoomsService>();
        builder.Services.AddSingleton<IGameLifecycleService, TestGameLifecycleService>();

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
