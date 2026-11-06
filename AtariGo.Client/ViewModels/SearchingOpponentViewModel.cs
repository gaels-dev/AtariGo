using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class SearchingOpponentViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan s_countdownRefreshInterval =
        TimeSpan.FromSeconds(1);

    private readonly INavigationService _navigationService;
    private readonly IMatchmakingClient _matchmakingClient;
    private readonly CancellationTokenSource _searchCancellation =
        new CancellationTokenSource();
    private readonly bool _isGuest;
    private readonly string _playerName;
    private readonly Task _searchTask;

    private string _opponentName;
    private string _statusMessage;
    private int _countdownSeconds;
    private DateTimeOffset _countdownEndsAt;
    private DispatcherTimer? _countdownTimer;
    private bool _showingRequeueNotice;
    private bool _isDisposed;

    public SearchingOpponentViewModel(
        INavigationService navigationService,
        IMatchmakingClient matchmakingClient,
        ClientSessionContext sessionContext)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(matchmakingClient);
        ArgumentNullException.ThrowIfNull(sessionContext);
        _navigationService = navigationService;
        _matchmakingClient = matchmakingClient;
        _isGuest = sessionContext.IsGuest;
        _playerName = sessionContext.PlayerName;
        _opponentName = GetLocalizedMessage("SearchingOpponent_Lbl_Searching");
        _statusMessage = GetLocalizedMessage("SearchingOpponent_Lbl_Searching");
        CancelCommand = new RelayCommand(CancelSearch);
        _searchTask = SearchPublicMatchAsync(_searchCancellation.Token);
    }

    public string PlayerName => _playerName;

    public string OpponentName
    {
        get => _opponentName;
        private set => SetProperty(ref _opponentName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int CountdownSeconds
    {
        get => _countdownSeconds;
        private set
        {
            if (SetProperty(ref _countdownSeconds, value))
            {
                OnPropertyChanged(nameof(CountdownMessage));
            }
        }
    }

    public string CountdownMessage
    {
        get
        {
            if (CountdownSeconds == 0)
            {
                return string.Empty;
            }

            string format = GetLocalizedMessage(
                "SearchingOpponent_Lbl_Countdown");
            return string.Format(
                CultureInfo.CurrentUICulture,
                format,
                CountdownSeconds);
        }
    }

    public ICommand CancelCommand { get; }

    public Task SearchTask => _searchTask;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopCountdown();
        _searchCancellation.Cancel();
        _searchCancellation.Dispose();
    }

    private async Task SearchPublicMatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (MatchmakingEvent matchmakingEvent in
                _matchmakingClient.SearchPublicMatchAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                HandleMatchmakingEvent(matchmakingEvent);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                StopCountdown();
                CountdownSeconds = 0;
                StatusMessage = GetLocalizedMessage(
                    "SearchingOpponent_Err_ConnectionClosed");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            StopCountdown();
            Trace.TraceInformation("Public matchmaking search was cancelled.");
        }
        catch (RpcException exception)
            when (GrpcCallPolicy.IsRequestedCancellation(
                exception,
                cancellationToken))
        {
            StopCountdown();
            Trace.TraceInformation("Public matchmaking stream was cancelled.");
        }
        catch (RpcException exception)
        {
            StopCountdown();
            CountdownSeconds = 0;
            StatusMessage = GrpcFailureMessages.GetMessage(exception);
        }
    }

    private void HandleMatchmakingEvent(MatchmakingEvent matchmakingEvent)
    {
        if (matchmakingEvent.EventCase
            == MatchmakingEvent.EventOneofCase.SearchQueued)
        {
            if (_showingRequeueNotice)
            {
                _showingRequeueNotice = false;
            }
            else
            {
                StatusMessage = GetLocalizedMessage("SearchingOpponent_Lbl_Searching");
            }
        }

        if (matchmakingEvent.EventCase
            == MatchmakingEvent.EventOneofCase.MatchCountdownStarted)
        {
            StartCountdown(matchmakingEvent.MatchCountdownStarted);
        }

        if (matchmakingEvent.EventCase
            == MatchmakingEvent.EventOneofCase.MatchCountdownCancelled)
        {
            StopCountdown();
            _showingRequeueNotice = true;
            OpponentName = GetLocalizedMessage("SearchingOpponent_Lbl_Searching");
            CountdownSeconds = 0;
            StatusMessage = GetLocalizedMessage(
                "SearchingOpponent_Msg_OpponentDisconnected");
        }

        if (matchmakingEvent.EventCase
            == MatchmakingEvent.EventOneofCase.PublicGameStarted)
        {
            StopCountdown();
            OpenGame(matchmakingEvent.PublicGameStarted);
        }
    }

    private void StartCountdown(MatchCountdownStarted countdown)
    {
        StopCountdown();
        OpponentName = countdown.OpponentName;
        StatusMessage = GetLocalizedMessage("SearchingOpponent_Lbl_MatchFound");
        _countdownEndsAt = DateTimeOffset.FromUnixTimeMilliseconds(
            countdown.CountdownEndsAtUnixMilliseconds);
        DispatcherTimer countdownTimer = new DispatcherTimer
        {
            Interval = s_countdownRefreshInterval
        };
        countdownTimer.Tick += OnCountdownTimerTick;
        _countdownTimer = countdownTimer;
        UpdateCountdownDisplay();
        countdownTimer.Start();
    }

    private void OnCountdownTimerTick(object? sender, EventArgs _)
    {
        UpdateCountdownDisplay();
    }

    private void UpdateCountdownDisplay()
    {
        TimeSpan remaining = _countdownEndsAt - DateTimeOffset.UtcNow;
        CountdownSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        if (remaining <= TimeSpan.Zero)
        {
            StopCountdown();
        }
    }

    private void StopCountdown()
    {
        if (_countdownTimer is null)
        {
            return;
        }

        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTimerTick;
        _countdownTimer = null;
    }

    private void OpenGame(PublicGameStarted game)
    {
        var navigationContext = new PublicMatchNavigationContext(
            game,
            _isGuest,
            _playerName);
        _navigationService.NavigateToPublicGame(navigationContext);
    }

    private void CancelSearch()
    {
        if (_isDisposed)
        {
            return;
        }

        _searchCancellation.Cancel();
        _navigationService.NavigateToMainMenu();
    }

    private static string GetLocalizedMessage(string key)
    {
        return Resources.ResourceManager.GetString(
            key,
            CultureInfo.CurrentUICulture)
            ?? key;
    }
}
