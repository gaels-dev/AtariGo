using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;


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
    public async Task SearchTask_CancelledMatch_ContinuesReadingStreamImmediately()
    {
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        CancelledMatchmakingClient matchmakingClient = new CancelledMatchmakingClient(
            testCountdownDuration);
        using SearchingOpponentViewModel viewModel = new SearchingOpponentViewModel(
            navigation,
            matchmakingClient,
            new ClientSessionContext { PlayerName = "Player" });

        await matchmakingClient.Requeued.WaitAsync(
            TimeSpan.FromSeconds(TestTimeoutSeconds));

        Assert.AreEqual(0, viewModel.CountdownSeconds);
        Assert.AreEqual(
            Resources.ResourceManager.GetString(
                "SearchingOpponent_Msg_OpponentDisconnected",
                CultureInfo.CurrentUICulture),
            viewModel.StatusMessage);
        viewModel.Dispose();
        await viewModel.SearchTask;
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
