using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class GameBoardViewModelTests
{
    [TestMethod]
    public async Task PlaceStoneCommand_OnlineMove_WaitsForServerBeforeDrawingStone()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        using GameBoardViewModel model = CreateModel(stream);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        await CommandExecution.ExecuteAsync(model.PlaceStoneCommand, model.Cells[0]);
        StoneColor beforeConfirmation = model.Cells[0].Stone;
        GameSessionClientEvent sent = stream.Sent.Single();
        GameSessionSnapshot confirmed = Snapshot(2);
        confirmed.Cells[0].Stone = GameStoneState.Black;
        confirmed.CurrentTurn = GameStoneState.White;
        confirmed.BlackRemainingMilliseconds = 30000;
        confirmed.WhiteRemainingMilliseconds = 30000;
        stream.Publish(new GameSessionServerEvent { Snapshot = confirmed });
        await WaitUntilAsync(() => model.Cells[0].Stone == StoneColor.Black);
        // Assert
        Assert.AreEqual(StoneColor.None, beforeConfirmation);
        Assert.AreEqual(0, sent.SubmitMove.Row);
        Assert.AreEqual(1, sent.SubmitMove.ExpectedVersion);
        Assert.AreEqual(StoneColor.White, model.CurrentTurn);
        Assert.IsFalse(model.IsMyTurn);
    }

    [TestMethod]
    public async Task PlaceStoneCommand_LostConnection_ReusesSameActionIdAfterReconnect()
    {
        // Arrange
        StubGameSessionStream first = new StubGameSessionStream { FailSend = true };
        StubGameSessionStream second = new StubGameSessionStream();
        using GameBoardViewModel model = CreateModel(first, second);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        await CommandExecution.ExecuteAsync(model.PlaceStoneCommand, model.Cells[1]);
        await WaitUntilAsync(() => second.Sent.Count == 1);
        // Assert
        Assert.AreEqual(first.Sent.Single().SubmitMove.ActionId,
            second.Sent.Single().SubmitMove.ActionId);
        Assert.AreEqual(first.Sent.Single().SubmitMove.ExpectedVersion,
            second.Sent.Single().SubmitMove.ExpectedVersion);
    }

    [TestMethod]
    public async Task SubmitSurrenderCommand_ActiveGame_SendsActionWithoutNavigatingAway()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        using GameBoardViewModel model = CreateModel(stream);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        await CommandExecution.ExecuteAsync(model.SubmitSurrenderCommand, null);
        // Assert
        Assert.IsNotNull(stream.Sent.Single().Surrender);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stream.Sent.Single().Surrender.ActionId));
    }

    [TestMethod]
    public async Task ApplySnapshot_OpponentsTurn_ShowsIndependentFullInactiveClock()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        using GameBoardViewModel model = CreateModel(stream);
        await WaitUntilAsync(() => model.IsMyTurn);
        GameSessionSnapshot snapshot = Snapshot(2);
        snapshot.CurrentTurn = GameStoneState.White;
        snapshot.BlackRemainingMilliseconds = 30000;
        snapshot.WhiteRemainingMilliseconds = 7000;
        // Act
        stream.Publish(new GameSessionServerEvent { Snapshot = snapshot });
        await WaitUntilAsync(() => model.CurrentTurn == StoneColor.White);
        // Assert
        Assert.AreEqual("00:30", model.BlackTimeRemaining);
        Assert.IsFalse(model.IsMyTurn);
    }

    [TestMethod]
    public async Task ApplySnapshot_DeadlineAlreadyPassed_ShowsZeroForActiveClock()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        using GameBoardViewModel model = CreateModel(stream);
        await WaitUntilAsync(() => model.IsMyTurn);
        GameSessionSnapshot snapshot = Snapshot(2);
        snapshot.CurrentTurn = GameStoneState.White;
        snapshot.ServerTimeUnixMilliseconds = 100000;
        snapshot.TurnEndsAtUnixMilliseconds = 99999;
        // Act
        stream.Publish(new GameSessionServerEvent { Snapshot = snapshot });
        await WaitUntilAsync(() => model.CurrentTurn == StoneColor.White);
        // Assert
        Assert.AreEqual("00:00", model.WhiteTimeRemaining);
        Assert.AreEqual("00:30", model.BlackTimeRemaining);
    }

    private static GameBoardViewModel CreateModel(StubGameSessionStream first,
        StubGameSessionStream? second = null)
    {
        StubGameSessionClient client = new StubGameSessionClient();
        client.Streams.Enqueue(first);
        first.Publish(new GameSessionServerEvent { Snapshot = Snapshot(1) });
        if (second is not null)
        {
            client.Streams.Enqueue(second);
            second.Publish(new GameSessionServerEvent { Snapshot = Snapshot(2) });
        }
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        GameBoardViewModel model = new GameBoardViewModel(navigation,
            new GameBoardViewModelContext(client, true, "Black"));
        model.OpenPublicGame(new PublicMatchNavigationContext(new PublicGameStarted
        {
            GameId = 1,
            StoneColor = "Black",
            CaptureGoal = 1,
            OpponentName = "White"
        }, true, "Black"));
        return model;
    }

    private static GameSessionSnapshot Snapshot(long version)
    {
        GameSessionSnapshot snapshot = new GameSessionSnapshot
        {
            GameId = 1,
            BoardSize = 9,
            CaptureGoal = 1,
            TurnTimeSeconds = 30,
            PlayerName = "Black",
            OpponentName = "White",
            PlayerStoneColor = "Black",
            OpponentStoneColor = "White",
            OpponentConnected = true,
            MaximumChatMessageLength = 100,
            Status = "InProgress",
            Version = version,
            CurrentTurn = GameStoneState.Black,
            BlackRemainingMilliseconds = 30000,
            WhiteRemainingMilliseconds = 30000
        };
        for (int row = 0; row < snapshot.BoardSize; row++)
        {
            for (int column = 0; column < snapshot.BoardSize; column++)
            {
                snapshot.Cells.Add(new GameBoardCell
                {
                    Row = row,
                    Column = column,
                    Stone = GameStoneState.Empty
                });
            }
        }
        return snapshot;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
