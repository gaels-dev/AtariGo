using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameSessionRegistryTests
{
    private const long SharedGameId = 42;

    [TestMethod]
    public async Task Connect_TwoPlayers_PublishesSharedConnectedStateAsync()
    {
        // Arrange

        GameSessionRegistry registry = new GameSessionRegistry(TimeProvider.System);
        GameSessionDefinition blackPlayerDefinition =
            CreateDefinition(("user:1", "Black"), ("user:2", "White"));

        // Act
        GameSessionConnection blackConnection = registry.Connect(
            blackPlayerDefinition);
        GameSessionSnapshot initialBlackSnapshot = await ReadSnapshotAsync(
            blackConnection);
        GameSessionConnection whiteConnection = registry.Connect(
            CreateDefinition(("user:2", "White"), ("user:1", "Black")));

        GameSessionSnapshot updatedBlackSnapshot = await ReadSnapshotAsync(
            blackConnection);
        GameSessionSnapshot initialWhiteSnapshot = await ReadSnapshotAsync(
            whiteConnection);

        // Assert
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
    public async Task Disconnect_Player_RemainingPlayerReceivesDisconnectedStateAsync()
    {
        // Arrange

        GameSessionRegistry registry = new GameSessionRegistry(TimeProvider.System);
        GameSessionConnection hostConnection = registry.Connect(
            CreateDefinition(("user:1", "Black"), ("user:2", "White")));
        GameSessionConnection opponentConnection = registry.Connect(
            CreateDefinition(("user:2", "White"), ("user:1", "Black")));
        await ReadSnapshotAsync(hostConnection);
        await ReadSnapshotAsync(hostConnection);
        await ReadSnapshotAsync(opponentConnection);

        // Act
        registry.Disconnect(opponentConnection);
        GameSessionSnapshot disconnectedSnapshot = await ReadSnapshotAsync(
            hostConnection);

        // Assert
        Assert.IsFalse(disconnectedSnapshot.OpponentConnected);

        Assert.IsTrue(disconnectedSnapshot.OpponentDisconnected);
        registry.Disconnect(hostConnection);
    }

    [TestMethod]
    public async Task Reconnect_SamePlayer_ReplacesOldStreamSafelyAsync()
    {
        // Arrange

        GameSessionRegistry registry = new GameSessionRegistry(TimeProvider.System);
        GameSessionDefinition definition =
            CreateDefinition(("user:1", "Black"), ("user:2", "White"));
        GameSessionConnection originalConnection = registry.Connect(definition);
        await ReadSnapshotAsync(originalConnection);

        // Act
        GameSessionConnection replacementConnection = registry.Connect(definition);
        await ReadSnapshotAsync(replacementConnection);
        registry.Disconnect(originalConnection);
        GameSessionConnection opponentConnection = registry.Connect(
            CreateDefinition(("user:2", "White"), ("user:1", "Black")));
        GameSessionSnapshot replacementSnapshot = await ReadSnapshotAsync(
            replacementConnection);

        // Assert
        Assert.IsTrue(replacementSnapshot.OpponentConnected);

        Assert.IsFalse(await originalConnection.Events.WaitToReadAsync());

        registry.Disconnect(opponentConnection);
        registry.Disconnect(replacementConnection);
    }

    [TestMethod]
    public async Task PublishChatMessage_ConnectedPlayers_ReceivesServerAuthoredMessageAsync()
    {
        // Arrange

        GameSessionRegistry registry = new GameSessionRegistry(TimeProvider.System);

        GameSessionConnection blackConnection = registry.Connect(
            CreateDefinition(("user:1", "Black"), ("user:2", "White")));
        GameSessionConnection whiteConnection = registry.Connect(
            CreateDefinition(("user:2", "White"), ("user:1", "Black")));
        await ReadSnapshotAsync(blackConnection);
        await ReadSnapshotAsync(blackConnection);
        await ReadSnapshotAsync(whiteConnection);

        // Act
        ChatMessageSubmissionResult result = registry.PublishChatMessage(
            blackConnection,
            "  Hello, opponent!  ");
        GameSessionDelivery senderDelivery = await blackConnection.Events.ReadAsync();
        GameSessionDelivery opponentDelivery = await whiteConnection.Events.ReadAsync();

        // Assert
        Assert.AreEqual(ChatMessageSubmissionResult.Accepted, result);

        Assert.AreEqual("user:1", senderDelivery.RecipientPlayerKey);

        Assert.AreEqual("user:2", opponentDelivery.RecipientPlayerKey);

        Assert.AreEqual("user:1", senderDelivery.ChatMessage?.SenderPlayerKey);

        Assert.AreEqual("user:1", opponentDelivery.ChatMessage?.SenderPlayerKey);

        Assert.AreEqual("Hello, opponent!", senderDelivery.ChatMessage?.Text);
        GameSessionConnection reconnectedBlack = registry.Connect(
            CreateDefinition(("user:1", "Black"), ("user:2", "White")));
        GameSessionSnapshot restoredSnapshot = await ReadSnapshotAsync(
            reconnectedBlack);

        Assert.AreEqual(1, restoredSnapshot.ChatHistory.Count);

        Assert.AreEqual("Hello, opponent!", restoredSnapshot.ChatHistory[0].Text);
        registry.Disconnect(blackConnection);
        registry.Disconnect(whiteConnection);
        registry.Disconnect(reconnectedBlack);
    }

    [TestMethod]
    public async Task PublishChatMessage_InvalidText_RejectsWithoutEndingConnectionAsync()
    {
        // Arrange

        GameSessionRegistry registry = new GameSessionRegistry(TimeProvider.System);
        GameSessionConnection connection = registry.Connect(
            CreateDefinition(("user:1", "Black"), ("user:2", "White")));
        await ReadSnapshotAsync(connection);
        string longMessage = new string(
            'x',
            GameSessionRules.MaximumChatMessageLength + 1);

        // Act
        ChatMessageSubmissionResult emptyResult = registry.PublishChatMessage(
            connection,
            "   ");
        GameSessionDelivery emptyRejection = await connection.Events.ReadAsync();
        ChatMessageSubmissionResult longResult = registry.PublishChatMessage(
            connection,
            longMessage);
        GameSessionDelivery lengthRejection = await connection.Events.ReadAsync();

        // Assert
        Assert.AreEqual(ChatMessageSubmissionResult.Empty, emptyResult);

        Assert.AreEqual(ChatMessageRejectionReason.Empty, emptyRejection.RejectionReason);

        Assert.AreEqual(ChatMessageSubmissionResult.TooLong, longResult);

        Assert.AreEqual(
            ChatMessageRejectionReason.TooLong,
            lengthRejection.RejectionReason);
        registry.Disconnect(connection);
    }

    private static async Task<GameSessionSnapshot> ReadSnapshotAsync(
        GameSessionConnection connection)
    {
        GameSessionDelivery delivery = await connection.Events.ReadAsync();
        return delivery.Snapshot
            ?? throw new InvalidOperationException("A game snapshot was expected.");
    }

    private static GameSessionDefinition CreateDefinition(
        (string Key, string Color) playerInfo,
        (string Key, string Color) opponentInfo)
    {
        GameSessionParticipant player = new GameSessionParticipant(
            playerInfo.Key,
            playerInfo.Key,
            playerInfo.Color);
        GameSessionParticipant opponent = new GameSessionParticipant(
            opponentInfo.Key,
            opponentInfo.Key,
            opponentInfo.Color);
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
