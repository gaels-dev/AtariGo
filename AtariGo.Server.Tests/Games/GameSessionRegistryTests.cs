using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameSessionRegistryTests
{
    private const long SharedGameId = 42;

    [TestMethod]
    public async Task Connect_TwoPlayers_PublishesSharedConnectedState()
    {
        GameSessionRegistry registry = new GameSessionRegistry();
        GameSessionDefinition blackPlayerDefinition = CreateDefinition(
            "user:1", "user:2", "Black", "White");
        GameSessionConnection blackConnection = registry.Connect(
            blackPlayerDefinition);
        GameSessionSnapshot initialBlackSnapshot = await blackConnection.Snapshots
            .ReadAsync();
        GameSessionConnection whiteConnection = registry.Connect(
            CreateDefinition("user:2", "user:1", "White", "Black"));

        GameSessionSnapshot updatedBlackSnapshot = await blackConnection.Snapshots
            .ReadAsync();
        GameSessionSnapshot initialWhiteSnapshot = await whiteConnection.Snapshots
            .ReadAsync();

        Assert.AreEqual(SharedGameId, initialBlackSnapshot.GameId);
        Assert.IsFalse(initialBlackSnapshot.OpponentConnected);
        Assert.IsTrue(updatedBlackSnapshot.OpponentConnected);
        Assert.IsTrue(initialWhiteSnapshot.OpponentConnected);
        Assert.AreEqual("Black", updatedBlackSnapshot.Player.StoneColor);
        Assert.AreEqual("White", initialWhiteSnapshot.Player.StoneColor);
        Assert.AreEqual(GameSessionRules.BoardSize * GameSessionRules.BoardSize,
            initialWhiteSnapshot.Cells.Count);
    }

    [TestMethod]
    public async Task Disconnect_Player_RemainingPlayerReceivesDisconnectedState()
    {
        GameSessionRegistry registry = new GameSessionRegistry();
        GameSessionConnection hostConnection = registry.Connect(
            CreateDefinition("user:1", "user:2", "Black", "White"));
        GameSessionConnection opponentConnection = registry.Connect(
            CreateDefinition("user:2", "user:1", "White", "Black"));
        await hostConnection.Snapshots.ReadAsync();
        await hostConnection.Snapshots.ReadAsync();
        await opponentConnection.Snapshots.ReadAsync();

        registry.Disconnect(opponentConnection);
        GameSessionSnapshot disconnectedSnapshot = await hostConnection.Snapshots
            .ReadAsync();

        Assert.IsFalse(disconnectedSnapshot.OpponentConnected);
        Assert.IsTrue(disconnectedSnapshot.OpponentDisconnected);
        registry.Disconnect(hostConnection);
    }

    [TestMethod]
    public async Task Reconnect_SamePlayer_ReplacesOldStreamSafely()
    {
        GameSessionRegistry registry = new GameSessionRegistry();
        GameSessionDefinition definition = CreateDefinition(
            "user:1", "user:2", "Black", "White");
        GameSessionConnection originalConnection = registry.Connect(definition);
        await originalConnection.Snapshots.ReadAsync();
        GameSessionConnection replacementConnection = registry.Connect(definition);
        await replacementConnection.Snapshots.ReadAsync();
        registry.Disconnect(originalConnection);
        GameSessionConnection opponentConnection = registry.Connect(
            CreateDefinition("user:2", "user:1", "White", "Black"));
        GameSessionSnapshot replacementSnapshot = await replacementConnection.Snapshots
            .ReadAsync();

        Assert.IsTrue(replacementSnapshot.OpponentConnected);
        Assert.IsFalse(await originalConnection.Snapshots.WaitToReadAsync());
        registry.Disconnect(opponentConnection);
        registry.Disconnect(replacementConnection);
    }

    private static GameSessionDefinition CreateDefinition(
        string playerKey,
        string opponentKey,
        string playerColor,
        string opponentColor)
    {
        GameSessionParticipant player = new GameSessionParticipant(
            playerKey,
            playerKey,
            playerColor);
        GameSessionParticipant opponent = new GameSessionParticipant(
            opponentKey,
            opponentKey,
            opponentColor);
        return new GameSessionDefinition
        {
            GameId = SharedGameId,
            CaptureGoal = 1,
            GameTimeSeconds = 600,
            TurnTimeSeconds = 30,
            Player = player,
            Opponent = opponent
        };
    }
}
