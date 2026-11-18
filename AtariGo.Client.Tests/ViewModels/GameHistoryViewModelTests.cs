using System.Reflection;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class GameHistoryViewModelTests
{
    [TestMethod]
    public async Task RefreshCommand_FailedRefresh_ClearsPreviousHistoryAndAllowsRetryAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        var client = new StubGameHistoryClient { Response = Task.FromResult(CreateHistory()) };
        using var viewModel = new GameHistoryViewModel(navigation, client);
        await CommandExecution.ExecuteAsync(viewModel.RefreshCommand, null);
        Assert.HasCount(1, viewModel.Entries);
        client.Response = Task.FromResult(new GameHistoryQueryResult
        {
            Failure = ClientFailureReason.Unavailable
        });
        // Act
        await CommandExecution.ExecuteAsync(viewModel.RefreshCommand, null);
        string failureMessage = viewModel.StatusMessage;
        client.Response = Task.FromResult(new GameHistoryQueryResult());
        await CommandExecution.ExecuteAsync(viewModel.RefreshCommand, null);
        viewModel.BackCommand.Execute(null);
        // Assert
        Assert.HasCount(0, viewModel.Entries);
        Assert.IsFalse(viewModel.IsLoading);
        Assert.IsTrue(viewModel.RefreshCommand.CanExecute(null));
        Assert.AreNotEqual(failureMessage, viewModel.StatusMessage);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        CollectionAssert.Contains(((RecordingNavigationProxy)navigation).Calls.ToArray(),
            nameof(INavigationService.NavigateToMainMenu));
    }

    [TestMethod]
    public async Task Dispose_PendingQuery_CancelsAndIgnoresLateHistoryAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        var pending = new TaskCompletionSource<GameHistoryQueryResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubGameHistoryClient { Response = pending.Task };
        using var viewModel = new GameHistoryViewModel(navigation, client);
        Task command = CommandExecution.ExecuteAsync(viewModel.RefreshCommand, null);
        string previousStatus = viewModel.StatusMessage;
        // Act
        viewModel.Dispose();
        pending.SetResult(CreateHistory());
        await command;
        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.HasCount(0, viewModel.Entries);
        Assert.AreEqual(previousStatus, viewModel.StatusMessage);
    }

    private static GameHistoryQueryResult CreateHistory()
    {
        return new GameHistoryQueryResult
        {
            Entries = new[]
            {
                new GameHistoryEntry
                {
                    GameId = 1, PlayedAtUtc = DateTimeOffset.UtcNow, OpponentName = "Rival",
                    Result = "Won", State = "Completed",
                    StoneColor = "Black", OpponentStoneColor = "White"
                }
            }
        };
    }
}
