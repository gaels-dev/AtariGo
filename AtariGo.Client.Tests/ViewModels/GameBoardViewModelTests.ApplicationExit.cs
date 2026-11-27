using System.Globalization;
using System.Reflection;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

public sealed partial class GameBoardViewModelTests
{
    [TestMethod]
    [DataRow("en", "close")]
    [DataRow("es", "cerrar")]
    [DataRow("de", "schließen")]
    public async Task RequestApplicationCloseAsync_SupportedCulture_LocalizesConfirmationAsync(
        string culture, string expectedWord)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            StubGameSessionStream stream = new StubGameSessionStream();
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
            using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
            await WaitForTurnAsync(model);

            Task<bool> closing = model.RequestApplicationCloseAsync(CancellationToken.None);
            ConfirmActionDialogViewModel dialog =
                (ConfirmActionDialogViewModel)recorded.OpenedDialog!;
            bool translated = dialog.Message.Contains(expectedWord,
                StringComparison.OrdinalIgnoreCase);
            dialog.Close(false);

            Assert.IsFalse(await closing);
            Assert.IsTrue(translated);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    public async Task RequestApplicationCloseAsync_CancelledConfirmation_KeepsGameOpenAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitForTurnAsync(model);

        Task<bool> closing = model.RequestApplicationCloseAsync(CancellationToken.None);
        ConfirmActionDialogViewModel dialog = (ConfirmActionDialogViewModel)recorded.OpenedDialog!;
        dialog.CancelCommand.Execute(null);
        bool allowed = await closing;

        Assert.IsFalse(allowed);
        Assert.IsFalse(stream.IsDisposed);
        Assert.IsTrue(stream.Sent.IsEmpty);
        Assert.IsTrue(model.CanSurrender);
        Assert.IsTrue(model.IsMyTurn);
        Task<bool> second = model.RequestApplicationCloseAsync(CancellationToken.None);
        recorded.OpenedDialog!.Close(false);
        Assert.IsFalse(await second);
    }

    [TestMethod]
    public async Task RequestApplicationCloseAsync_Confirmed_WaitsForServerAndDoesNotNavigateAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitForTurnAsync(model);

        Task<bool> closing = model.RequestApplicationCloseAsync(CancellationToken.None);
        recorded.OpenedDialog!.Close(true);
        bool HasSentSurrender()
        {
            return stream.Sent.Count == 1;
        }
        await WaitUntilAsync(HasSentSurrender);
        bool waited = !closing.IsCompleted && !stream.IsDisposed;
        stream.Publish(AcceptSurrender(stream));
        bool allowed = await closing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(allowed);
        Assert.IsTrue(waited);
        Assert.IsTrue(stream.IsCompleting);
        Assert.IsFalse(stream.WasCancelledBeforeCompletion);
        Assert.IsFalse(model.CanLeaveGame);
        Assert.IsFalse(recorded.Calls.Contains(nameof(INavigationService.NavigateToMainMenu)));
    }

    [TestMethod]
    public async Task
        RequestApplicationCloseAsync_GameFinishesDuringPrompt_ClosesWithoutSurrenderAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        using GameBoardViewModel model = CreateModel(stream);
        await WaitForTurnAsync(model);

        Task<bool> closing = model.RequestApplicationCloseAsync(CancellationToken.None);
        stream.Publish(FinishedGame());
        bool allowed = await closing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(allowed);
        Assert.IsTrue(stream.Sent.IsEmpty);
        Assert.IsTrue(stream.IsCompleting);
    }

    [TestMethod]
    public async Task RequestApplicationCloseAsync_AlreadyFinished_DoesNotAskAgainAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using GameBoardViewModel model = CreateModel(stream, navigation: navigation);
        await WaitForTurnAsync(model);
        stream.Publish(FinishedGame());
        bool IsGameOverDialogOpen()
        {
            return recorded.OpenedDialog is GameOverDialogViewModel;
        }
        await WaitUntilAsync(IsGameOverDialogOpen);
        DialogViewModelBase previous = recorded.OpenedDialog!;

        bool allowed = await model.RequestApplicationCloseAsync(CancellationToken.None);

        Assert.IsTrue(allowed);
        Assert.AreSame(previous, recorded.OpenedDialog);
        Assert.IsTrue(stream.Sent.IsEmpty);
        Assert.IsTrue(stream.IsCompleting);
    }

    [TestMethod]
    public async Task RequestApplicationCloseAsync_MainViewModel_RespectsGameCancellationAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RecordingNavigationProxy recorded = (RecordingNavigationProxy)navigation;
        using MainViewModel main = new MainViewModel(
            new ClientSessionContext { IsGuest = true, PlayerName = "Guest" }, navigation);
        GameBoardViewModel game = CreateModel(stream, navigation: navigation);
        main.CurrentViewModel = game;
        await WaitForTurnAsync(game);

        Task<bool> closing = main.RequestApplicationCloseAsync(CancellationToken.None);
        recorded.OpenedDialog!.Close(false);

        Assert.IsFalse(await closing);
        Assert.AreSame(game, main.CurrentViewModel);
        Assert.IsFalse(stream.IsDisposed);
        Assert.IsTrue(game.CanSurrender);
    }

    private static Task WaitForTurnAsync(GameBoardViewModel model)
    {
        bool HasTurn()
        {
            return model.IsMyTurn;
        }
        return WaitUntilAsync(HasTurn);
    }
}
