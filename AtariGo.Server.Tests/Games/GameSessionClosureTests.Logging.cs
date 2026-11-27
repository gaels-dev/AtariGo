using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;

namespace AtariGo.Server.Tests.Games;

public sealed partial class GameSessionClosureTests
{
    [TestMethod]
    public async Task Connect_RepeatedSurrender_LogsOneAcceptedSurrenderAfterPersistenceAsync()
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
        GameSessionClientEvent surrender = await FinishGameAsync(host.Database.GameId,
            black, white);

        await black.RequestStream.WriteAsync(surrender);
        await black.RequestStream.CompleteAsync();
        await DrainAsync(black.ResponseStream, timeout.Token);
        await white.RequestStream.CompleteAsync();
        await DrainAsync(white.ResponseStream, timeout.Token);

        int accepted = 0;
        foreach ((LogLevel level, string message, Exception? exception) in logs.Entries)
        {
            AssertNoPlayerIdentity(message, host);
            if (message.StartsWith("Surrender accepted.", StringComparison.Ordinal))
            {
                Assert.AreEqual(LogLevel.Information, level);
                Assert.IsTrue(message.Contains("StoneColor: Black", StringComparison.Ordinal));
                Assert.IsTrue(message.Contains("Winner: White", StringComparison.Ordinal));
                accepted++;
            }
        }
        Assert.AreEqual(1, accepted);
        Assert.AreEqual("Surrender", (await host.Database.ReadAsync()).EndReason);
    }

    [TestMethod]
    public async Task Connect_InterruptedGame_LogsGracePeriodAndWinnerWhenItExpiresAsync()
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
        GameSessionClientEvent join = new GameSessionClientEvent
        {
            JoinGame = new JoinGame { GameId = host.Database.GameId }
        };
        await black.RequestStream.WriteAsync(join);
        await white.RequestStream.WriteAsync(join);
        await ReadStatusAsync(black.ResponseStream, "InProgress");
        await ReadStatusAsync(white.ResponseStream, "InProgress");

        black.Dispose();
        await logs.DisconnectWarning.WaitAsync(timeout.Token);
        host.Database.Time.Advance(TimeSpan.FromSeconds(30));
        await host.Coordinator.AdvanceAsync(host.Database.GameId, timeout.Token);
        await ReadStatusAsync(white.ResponseStream, "Completed");
        await white.RequestStream.CompleteAsync();
        await DrainAsync(white.ResponseStream, timeout.Token);

        int warnings = 0;
        int finishes = 0;
        foreach ((LogLevel level, string message, Exception? exception) in logs.Entries)
        {
            AssertNoPlayerIdentity(message, host);
            if (level == LogLevel.Warning)
            {
                Assert.IsTrue(message.Contains("StoneColor: Black", StringComparison.Ordinal));
                Assert.IsTrue(message.Contains("BlackReconnectDeadlineUtc:",
                    StringComparison.Ordinal));
                Assert.IsNull(exception);
                warnings++;
            }
            if (message.StartsWith("Game finished.", StringComparison.Ordinal))
            {
                Assert.AreEqual(LogLevel.Information, level);
                Assert.IsTrue(message.Contains("DisconnectionTimeout", StringComparison.Ordinal));
                Assert.IsTrue(message.Contains("Winner: White", StringComparison.Ordinal));
                finishes++;
            }
            Assert.IsFalse(message.Contains("Error reading message", StringComparison.Ordinal));
        }
        Assert.AreEqual(1, warnings);
        Assert.AreEqual(1, finishes);
        Assert.AreEqual("DisconnectionTimeout", (await host.Database.ReadAsync()).EndReason);
    }

    private static void AssertNoPlayerIdentity(string message, GameplayGrpcTestHost host)
    {
        Assert.IsFalse(message.Contains("PlayerKey", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("GamePlayerId", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("user:", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("guest:", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(host.BlackIdentity.Credential, StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(host.WhiteIdentity.Credential, StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(host.BlackIdentity.Identity.GuestId.ToString("N"),
            StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(host.WhiteIdentity.Identity.GuestId.ToString("N"),
            StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(host.BlackIdentity.Identity.GuestId.ToString("D"),
            StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(host.WhiteIdentity.Identity.GuestId.ToString("D"),
            StringComparison.Ordinal));
    }
}
