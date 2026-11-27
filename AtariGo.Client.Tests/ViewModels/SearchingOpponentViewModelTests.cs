using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class SearchingOpponentViewModelTests
{
    private const int TestTimeoutSeconds = 5;
    private static readonly TimeSpan testCountdownDuration = TimeSpan.FromMinutes(1);

    [TestMethod]
    public async Task SearchTask_CancelledMatch_ContinuesReadingStreamImmediatelyAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        CancelledMatchmakingClient matchmakingClient = new CancelledMatchmakingClient(
            testCountdownDuration);
        using SearchingOpponentViewModel viewModel = new SearchingOpponentViewModel(
            navigation,
            matchmakingClient,
            new ClientSessionContext { PlayerName = "Player" });

        // Act
        await matchmakingClient.Requeued.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));

        // Assert
        Assert.AreEqual(0, viewModel.CountdownSeconds);
        Assert.AreEqual(
            Resources.ResourceManager.GetString(
                "SearchingOpponent_Msg_OpponentDisconnected",
                CultureInfo.CurrentUICulture),
            viewModel.StatusMessage);
        viewModel.Dispose();
        await viewModel.SearchTask;
    }

    [TestMethod]
    public async Task CancelCommand_WaitingForMatch_CancelsSearchAndReturnsToMenuAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        CancelledMatchmakingClient matchmakingClient = new CancelledMatchmakingClient(
            testCountdownDuration);
        using SearchingOpponentViewModel viewModel = new SearchingOpponentViewModel(
            navigation, matchmakingClient,
            new ClientSessionContext { PlayerName = "Player" });
        await matchmakingClient.Requeued.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));

        // Act
        viewModel.CancelCommand.Execute(null);
        await viewModel.SearchTask;

        // Assert
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.NavigateToMainMenu) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
        Assert.AreEqual(0, viewModel.CountdownSeconds);
    }

    [TestMethod]
    public async Task SearchTask_PublicGameStarted_NavigatesWithPlayerIdentityAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        PublicGameStarted game = new PublicGameStarted
        {
            GameId = 42,
            OpponentName = "Opponent",
            StoneColor = "Black"
        };
        using SearchingOpponentViewModel viewModel = new SearchingOpponentViewModel(
            navigation, new StartedMatchmakingClient(game),
            new ClientSessionContext { PlayerName = "Guest-Player", IsGuest = true });

        // Act
        await viewModel.SearchTask;

        // Assert
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.NavigateToPublicGame) },
            recorder.Calls.ToArray());
        PublicMatchNavigationContext context = Assert.IsInstanceOfType<
            PublicMatchNavigationContext>(recorder.LastNavigationArgument);
        Assert.AreSame(game, context.Game);
        Assert.IsTrue(context.IsGuest);
        Assert.AreEqual("Guest-Player", context.PlayerName);
    }

    [TestMethod]
    public async Task SearchTask_TransportUnavailable_ShowsErrorWithoutNavigatingAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using SearchingOpponentViewModel viewModel = new SearchingOpponentViewModel(
            navigation, new UnavailableMatchmakingClient(),
            new ClientSessionContext { PlayerName = "Player" });

        // Act
        await viewModel.SearchTask;

        // Assert
        Assert.AreEqual(Resources.ResourceManager.GetString(
            "Connection_Err_Unavailable", CultureInfo.CurrentUICulture),
            viewModel.StatusMessage);
        Assert.AreEqual(0, viewModel.CountdownSeconds);
        Assert.HasCount(0, ((RecordingNavigationProxy)navigation).Calls);
    }

    private sealed class UnavailableMatchmakingClient : IMatchmakingClient
    {
        public async IAsyncEnumerable<MatchmakingEvent> SearchPublicMatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new MatchmakingEvent { SearchQueued = new SearchQueued() };
            throw new RpcException(new Status(StatusCode.Unavailable,
                "Matchmaking service unavailable."));
        }
    }

    private sealed class StartedMatchmakingClient : IMatchmakingClient
    {
        private readonly PublicGameStarted _game;

        public StartedMatchmakingClient(PublicGameStarted game)
        {
            _game = game;
        }

        public async IAsyncEnumerable<MatchmakingEvent> SearchPublicMatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new MatchmakingEvent { PublicGameStarted = _game };
        }
    }

    private sealed class CancelledMatchmakingClient : IMatchmakingClient
    {
        private readonly TimeSpan _countdownDuration;
        private readonly TaskCompletionSource<bool> _requeued =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public CancelledMatchmakingClient(TimeSpan countdownDuration)
        {
            _countdownDuration = countdownDuration;
        }

        public Task Requeued => _requeued.Task;

        public async IAsyncEnumerable<MatchmakingEvent> SearchPublicMatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new MatchmakingEvent
            {
                SearchQueued = new SearchQueued()
            };
            yield return new MatchmakingEvent
            {
                MatchCountdownStarted = new MatchCountdownStarted
                {
                    OpponentName = "Opponent",
                    CountdownEndsAtUnixMilliseconds = DateTimeOffset.UtcNow
                        .Add(_countdownDuration)
                        .ToUnixTimeMilliseconds()
                }
            };
            yield return new MatchmakingEvent
            {
                MatchCountdownCancelled = new MatchCountdownCancelled()
            };
            yield return new MatchmakingEvent
            {
                SearchQueued = new SearchQueued()
            };
            _requeued.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
