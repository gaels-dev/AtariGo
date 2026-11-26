using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameSessionClosureTests
{
    [TestMethod]
    public async Task
        Connect_CompletedGameAndRequestCompleted_ClosesWithoutWarningOrReadErrorAsync()
    {
        GameStreamLogProvider logs = new GameStreamLogProvider();
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync(logs);
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> black =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> white =
            client.Connect(Options(host.WhiteIdentity.Credential, timeout.Token));
        await FinishGameAsync(host.Database.GameId, black, white);
        long version = (await host.Database.ReadAsync()).StateVersion;

        await black.RequestStream.CompleteAsync();
        await DrainAsync(black.ResponseStream, timeout.Token);
        await white.RequestStream.CompleteAsync();
        await DrainAsync(white.ResponseStream, timeout.Token);

        Assert.AreEqual(StatusCode.OK, black.GetStatus().StatusCode);
        Assert.AreEqual(StatusCode.OK, white.GetStatus().StatusCode);
        Assert.AreEqual(version, (await host.Database.ReadAsync()).StateVersion);
        int cleanClosures = 0;
        foreach ((LogLevel level, string message, Exception? exception) in logs.Entries)
        {
            Assert.IsTrue(level < LogLevel.Warning, message);
            Assert.IsNull(exception, message);
            Assert.IsFalse(message.Contains("Error reading message", StringComparison.Ordinal));
            if (message.StartsWith("Completed game stream closed gracefully.",
                StringComparison.Ordinal))
            {
                cleanClosures++;
            }
        }
        Assert.AreEqual(2, cleanClosures);
    }

    [TestMethod]
    public async Task Connect_CompletedGameAndTransportAborted_PreservesDisconnectWarningAsync()
    {
        GameStreamLogProvider logs = new GameStreamLogProvider();
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync(logs);
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> black =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> white =
            client.Connect(Options(host.WhiteIdentity.Credential, timeout.Token));
        await FinishGameAsync(host.Database.GameId, black, white);
        long version = (await host.Database.ReadAsync()).StateVersion;

        black.Dispose();
        await logs.DisconnectWarning.WaitAsync(timeout.Token);
        await white.RequestStream.CompleteAsync();
        await DrainAsync(white.ResponseStream, timeout.Token);

        Assert.AreEqual(version, (await host.Database.ReadAsync()).StateVersion);
        Assert.AreEqual("Completed", (await host.Database.ReadAsync()).State);
    }

    private static async Task FinishGameAsync(long gameId,
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> black,
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> white)
    {
        GameSessionClientEvent join = new GameSessionClientEvent
        {
            JoinGame = new JoinGame { GameId = gameId }
        };
        await black.RequestStream.WriteAsync(join);
        await white.RequestStream.WriteAsync(join);
        await ReadStatusAsync(black.ResponseStream, "InProgress");
        await ReadStatusAsync(white.ResponseStream, "InProgress");
        await black.RequestStream.WriteAsync(new GameSessionClientEvent
        {
            Surrender = new SurrenderGame { ActionId = Guid.NewGuid().ToString("N") }
        });
        await ReadStatusAsync(black.ResponseStream, "Completed");
        await ReadStatusAsync(white.ResponseStream, "Completed");
    }

    private static async Task ReadStatusAsync(IAsyncStreamReader<GameSessionServerEvent> reader,
        string status)
    {
        while (await reader.MoveNext(CancellationToken.None))
        {
            if (reader.Current.Snapshot?.Status == status)
            {
                return;
            }
        }
        Assert.Fail("The expected game state was not received.");
    }

    private static async Task DrainAsync(IAsyncStreamReader<GameSessionServerEvent> reader,
        CancellationToken cancellationToken)
    {
        while (await reader.MoveNext(cancellationToken))
        {
            Assert.IsNotNull(reader.Current);
        }
    }

    private static CallOptions Options(string credential, CancellationToken cancellationToken)
    {
        Metadata headers = new Metadata { { "x-guest-credential", credential } };
        return new CallOptions(headers, cancellationToken: cancellationToken);
    }
}
