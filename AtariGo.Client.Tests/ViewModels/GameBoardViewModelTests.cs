using System.Reflection;


using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Client.ViewModels.Dialogs;
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
        await WaitUntilAsync(() => model.WhiteTimeRemaining == "00:00");
        // Assert
        Assert.AreEqual("00:00", model.WhiteTimeRemaining);
        Assert.AreEqual("00:30", model.BlackTimeRemaining);
    }

    [TestMethod]
    [DataRow(StatusCode.Unauthenticated)]
    [DataRow(StatusCode.PermissionDenied)]
    [DataRow(StatusCode.InvalidArgument)]
    [DataRow(StatusCode.AlreadyExists)]
    public async Task LeaveGameCommand_PermanentFailure_ReturnsWithoutSurrender(StatusCode status)
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        stream.Fail(new RpcException(new Status(status, "Simulated permanent failure.")));
        await WaitUntilAsync(() => stream.IsDisposed && model.CanLeaveGame);
        model.LeaveGameCommand.Execute(null);
        model.LeaveGameCommand.Execute(null);
        // Assert
        Assert.AreEqual(1, recorded.Calls.Count(
            call => call == nameof(INavigationService.NavigateToMainMenu)));
        Assert.IsNull(recorded.OpenedDialog);
        Assert.AreEqual(0, stream.Sent.Count);
        Assert.IsFalse(model.IsMyTurn);
        Assert.IsFalse(model.CanSurrender);
    }

    [TestMethod]
    public void LeaveGameCommand_InitialConnectionRejected_StopsRetryingAndAllowsExit()
    {
        // Arrange
        StubGameSessionClient client = new StubGameSessionClient
        {
            ConnectFailure = new RpcException(new Status(StatusCode.PermissionDenied, "Denied."))
        };
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using GameBoardViewModel model = OpenModel(client, navigation);
        // Act
        model.LeaveGameCommand.Execute(null);
        // Assert
        Assert.AreEqual(1, client.ConnectCount);
        Assert.IsTrue(((RecordingNavigationProxy)navigation).Calls.Contains(
            nameof(INavigationService.NavigateToMainMenu)));
    }

    [TestMethod]
    public async Task LeaveGameCommand_ActiveGame_WaitsForAcceptedSurrenderAndStopsStream()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        model.LeaveGameCommand.Execute(null);
        ConfirmActionDialogViewModel dialog = (ConfirmActionDialogViewModel)recorded.OpenedDialog!;
        dialog.ConfirmCommand.Execute(null);
        await WaitUntilAsync(() => stream.Sent.Count == 1);
        bool navigatedBeforeConfirmation = recorded.Calls.Contains(
            nameof(INavigationService.NavigateToMainMenu));
        stream.Publish(AcceptSurrender(stream));
        await WaitUntilAsync(() => stream.IsDisposed);
        // Assert
        Assert.IsFalse(navigatedBeforeConfirmation);
        Assert.IsTrue(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
        Assert.IsTrue(stream.ReadCancellation.IsCancellationRequested);
        Assert.IsFalse(model.CanLeaveGame);
    }

    [TestMethod]
    public async Task SurrenderCommand_ConfirmedAndAccepted_KeepsResultScreenOpen()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        model.SurrenderCommand.Execute(null);
        recorded.OpenedDialog!.Close(true);
        await WaitUntilAsync(() => stream.Sent.Count == 1);
        stream.Publish(FinishedGame());
        stream.Publish(AcceptSurrender(stream));
        await WaitUntilAsync(() => model.ActionStatusMessage.Length == 0 && model.CanLeaveGame);
        // Assert
        Assert.IsFalse(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
        Assert.IsFalse(model.CanSurrender);
        Assert.IsFalse(stream.IsDisposed);
    }

    [TestMethod]
    public async Task LeaveGameCommand_ConfirmationCancelled_DoesNotSendOrNavigate()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        model.LeaveGameCommand.Execute(null);
        recorded.OpenedDialog!.Close(false);
        // Assert
        Assert.AreEqual(0, stream.Sent.Count);
        Assert.IsFalse(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
        Assert.IsTrue(model.CanSurrender);
    }

    [TestMethod]
    public async Task LeaveGameCommand_RejectedSurrender_StaysOnBoardAndAllowsRetry()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        model.LeaveGameCommand.Execute(null);
        recorded.OpenedDialog!.Close(true);
        await WaitUntilAsync(() => stream.Sent.Count == 1);
        GameSessionServerEvent rejected = AcceptSurrender(stream);
        rejected.ActionResult.IsAccepted = false;
        rejected.ActionResult.Rejection = "InvalidAction";
        // Act
        stream.Publish(rejected);
        await WaitUntilAsync(() => model.CanSurrender);
        // Assert
        Assert.IsFalse(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
        Assert.IsFalse(string.IsNullOrEmpty(model.ActionStatusMessage));
        Assert.IsTrue(model.CanLeaveGame);
    }

    [TestMethod]
    public async Task LeaveGameCommand_FinalSnapshot_ReturnsWithoutAnotherAction()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        stream.Publish(FinishedGame());
        await WaitUntilAsync(() => model.CanLeaveGame && !model.CanSurrender);
        model.LeaveGameCommand.Execute(null);
        // Assert
        Assert.IsTrue(((RecordingNavigationProxy)navigation).Calls.Contains(
            nameof(INavigationService.NavigateToMainMenu)));
        Assert.AreEqual(0, stream.Sent.Count);
    }

    [TestMethod]
    public async Task LeaveGameCommand_SurrenderSendFails_ReusesActionAndExitsAfterRecovery()
    {
        // Arrange
        StubGameSessionStream first = new StubGameSessionStream { FailSend = true };
        StubGameSessionStream second = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(first, second, navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        model.LeaveGameCommand.Execute(null);
        recorded.OpenedDialog!.Close(true);
        await WaitUntilAsync(() => first.IsDisposed);
        bool couldLeaveWhileRecovering = model.CanLeaveGame;
        await WaitUntilAsync(() => second.Sent.Count == 1);
        second.Publish(AcceptSurrender(second));
        await WaitUntilAsync(() => second.IsDisposed);
        // Assert
        Assert.IsFalse(couldLeaveWhileRecovering);
        Assert.AreEqual(first.Sent.Single().Surrender.ActionId,
            second.Sent.Single().Surrender.ActionId);
        Assert.IsTrue(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
    }

    [TestMethod]
    public async Task LeaveGameCommand_PendingMoveAndPermanentFailure_ExitsWithoutSurrender()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        await CommandExecution.ExecuteAsync(model.PlaceStoneCommand, model.Cells[0]);
        // Act
        stream.Fail(new RpcException(new Status(StatusCode.AlreadyExists, "Connection replaced.")));
        await WaitUntilAsync(() => stream.IsDisposed && model.CanLeaveGame);
        model.LeaveGameCommand.Execute(null);
        // Assert
        Assert.IsTrue(((RecordingNavigationProxy)navigation).Calls.Contains(
            nameof(INavigationService.NavigateToMainMenu)));
        Assert.IsNotNull(stream.Sent.Single().SubmitMove);
        Assert.AreEqual(StoneColor.None, model.Cells[0].Stone);
    }

    [TestMethod]
    public async Task LeaveGameCommand_PermanentSendFailure_ExitsWithoutResendingSurrender()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream
        {
            SendFailure = new RpcException(new Status(StatusCode.PermissionDenied,
                "Access denied."))
        };
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitUntilAsync(() => model.IsMyTurn);
        // Act
        model.LeaveGameCommand.Execute(null);
        recorded.OpenedDialog!.Close(true);
        await WaitUntilAsync(() => stream.IsDisposed && model.CanLeaveGame);
        bool navigatedBeforeExit = recorded.Calls.Contains(
            nameof(INavigationService.NavigateToMainMenu));
        model.LeaveGameCommand.Execute(null);
        // Assert
        Assert.IsFalse(navigatedBeforeExit);
        Assert.IsTrue(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
        Assert.AreEqual(1, stream.Sent.Count);
        Assert.IsNotNull(stream.Sent.Single().Surrender);
    }

    [TestMethod]
    public void LeaveGameCommand_MissingSessionClient_AllowsLocalExit()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using GameBoardViewModel model = OpenModel(null, navigation);
        bool couldLeave = model.CanLeaveGame;
        // Act
        model.LeaveGameCommand.Execute(null);
        // Assert
        Assert.IsTrue(couldLeave);
        Assert.IsTrue(((RecordingNavigationProxy)navigation).Calls.Contains(
            nameof(INavigationService.NavigateToMainMenu)));
    }

    private static GameSessionServerEvent AcceptSurrender(StubGameSessionStream stream)
    {
        return new GameSessionServerEvent
        {
            ActionResult = new GameActionResponse
            {
                ActionId = stream.Sent.Single().Surrender.ActionId,
                IsAccepted = true
            }
        };
    }

    private static GameSessionServerEvent FinishedGame()
    {
        GameSessionSnapshot snapshot = Snapshot(2);
        snapshot.Status = "Completed";
        snapshot.Winner = GameStoneState.White;
        snapshot.EndReason = "Surrender";
        return new GameSessionServerEvent { Snapshot = snapshot };
    }

    private static GameBoardViewModel CreateModel(StubGameSessionStream first,
        StubGameSessionStream? second = null, INavigationService? navigation = null)
    {
        StubGameSessionClient client = new StubGameSessionClient();
        client.Streams.Enqueue(first);
        first.Publish(new GameSessionServerEvent { Snapshot = Snapshot(1) });
        if (second is not null)
        {
            client.Streams.Enqueue(second);
            second.Publish(new GameSessionServerEvent { Snapshot = Snapshot(2) });
        }
        navigation ??= DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        return OpenModel(client, navigation);
    }

    private static GameBoardViewModel OpenModel(IGameSessionClient? client,
        INavigationService navigation)
    {
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
