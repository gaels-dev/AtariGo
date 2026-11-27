using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels;

public sealed class GameHistoryViewModel : ViewModelBase, IDisposable
{
    private readonly IGameHistoryClient _client;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly CancellationToken _token;
    private bool _isDisposed;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public GameHistoryViewModel(INavigationService navigation, IGameHistoryClient client)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _token = _lifetime.Token;
        RefreshCommand = new AsyncRelayCommand(LoadHistoryAsync, HandleFailure, _token);
        BackCommand = new RelayCommand(navigation.NavigateToMainMenu);
    }

    public ObservableCollection<GameHistoryEntryViewModel> Entries { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string TitleLabel => Localize("GameHistory_Lbl_Title");

    public string DescriptionLabel => Localize("GameHistory_Lbl_Description");

    public string DateLabel => Localize("GameHistory_Col_Date");

    public string OpponentLabel => Localize("GameHistory_Col_Opponent");

    public string ResultLabel => Localize("GameHistory_Col_Result");

    public string StoneColorLabel => Localize("GameHistory_Col_YourStones");

    public string OpponentStoneColorLabel => Localize("GameHistory_Col_OpponentStones");

    public string RefreshLabel => Localize("GameHistory_Btn_Refresh");

    public string BackLabel => Localize("GameHistory_Btn_Back");

    public ICommand RefreshCommand
    {
        get;
    }

    public ICommand BackCommand
    {
        get;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task LoadHistoryAsync(object? parameter)
    {
        if (_isDisposed || IsLoading)
        {
            return;
        }
        IsLoading = true;
        Entries.Clear();
        StatusMessage = Localize("GameHistory_Msg_Loading");
        try
        {
            GameHistoryQueryResult result = await _client.ListGamesByPlayerAsync(_token);
            _token.ThrowIfCancellationRequested();
            ApplyResult(result);
        }
        finally
        {
            if (!_isDisposed)
            {
                IsLoading = false;
            }
        }
    }

    private void ApplyResult(GameHistoryQueryResult result)
    {
        if (result.Failure != ClientFailureReason.None)
        {
            string key = result.Failure == ClientFailureReason.SessionExpired
                ? "Connection_Err_SessionExpired" : "GameHistory_Err_Unavailable";
            StatusMessage = Localize(key);
            return;
        }
        GameHistoryEntryViewModel[] entries = result.Entries
            .Select(entry => new GameHistoryEntryViewModel(entry)).ToArray();
        foreach (GameHistoryEntryViewModel entry in entries)
        {
            Entries.Add(entry);
        }
        StatusMessage = Entries.Count == 0 ? Localize("GameHistory_Msg_Empty") : string.Empty;
    }

    private void HandleFailure(Exception exception)
    {
        if (_isDisposed || _token.IsCancellationRequested)
        {
            return;
        }
        Trace.TraceWarning("Game history presentation failed with {0}.",
            exception.GetType().Name);
        Entries.Clear();
        StatusMessage = Localize("GameHistory_Err_Unavailable");
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
