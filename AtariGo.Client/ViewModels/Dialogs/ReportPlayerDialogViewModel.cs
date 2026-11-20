using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class ReportPlayerDialogViewModel : DialogViewModelBase, IDisposable
{
    private const int MaximumDescriptionLength = 2000;

    private readonly IReportClient _client;
    private readonly long _gameId;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly CancellationToken _token;
    private string _targetPlayerName;
    private ReportCategoryOption _selectedReason;
    private string _details = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isSubmitted;
    private bool _isDisposed;

    public ReportPlayerDialogViewModel(
        string targetPlayerName, long gameId, IReportClient client)
        : base(Localize("ReportPlayer_Lbl_Title"))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPlayerName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        ArgumentNullException.ThrowIfNull(client);
        _targetPlayerName = targetPlayerName;
        _gameId = gameId;
        _client = client;
        _token = _lifetime.Token;
        Reasons = new List<ReportCategoryOption>
        {
            new(PlayerReportCategory.Chat, Localize("ReportPlayer_Reason_Chat")),
            new(PlayerReportCategory.Inactivity,
                Localize("ReportPlayer_Reason_Inactivity")),
            new(PlayerReportCategory.Manipulation,
                Localize("ReportPlayer_Reason_Manipulation"))
        };
        _selectedReason = Reasons[0];
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, HandleFailure, _token);
        CancelCommand = new RelayCommand(() => Close(false));
    }

    public string TargetPlayerName
    {
        get => _targetPlayerName;
        set => SetProperty(ref _targetPlayerName, value);
    }

    public IReadOnlyList<ReportCategoryOption> Reasons { get; }

    public ReportCategoryOption SelectedReason
    {
        get => _selectedReason;
        set => SetProperty(ref _selectedReason, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CancelLabel => _isSubmitted
        ? Localize("ReportPlayer_Btn_Close")
        : Localize("ReportPlayer_Btn_Cancel");

    public int MaximumDetailsLength => MaximumDescriptionLength;

    public bool CanSubmit => !_isDisposed && !_isSubmitted;

    public ICommand SubmitCommand { get; }

    public ICommand CancelCommand { get; }

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

    private async Task SubmitAsync(object? parameter)
    {
        if (!CanSubmit)
        {
            return;
        }
        if (Details.Trim().Length > MaximumDescriptionLength)
        {
            StatusMessage = Localize("ReportPlayer_Err_TooLong");
            return;
        }
        PlayerReportDraft draft = new PlayerReportDraft(
            _gameId, SelectedReason.Category, Details);
        PlayerReportResult result = await _client.SubmitReportAsync(draft, _token);
        _token.ThrowIfCancellationRequested();
        StatusMessage = GetResultMessage(result);
        if (result.Failure == ClientFailureReason.None
            && result.Status is PlayerReportStatus.Accepted
                or PlayerReportStatus.AlreadyReported)
        {
            _isSubmitted = true;
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CancelLabel));
        }
    }

    private static string GetResultMessage(PlayerReportResult result)
    {
        if (result.Failure == ClientFailureReason.SessionExpired)
        {
            return Localize("Connection_Err_SessionExpired");
        }
        if (result.Failure != ClientFailureReason.None)
        {
            return Localize("ReportPlayer_Err_Unavailable");
        }
        return Localize("ReportPlayer_Result_" + result.Status);
    }

    private void HandleFailure(Exception exception)
    {
        if (_isDisposed || _token.IsCancellationRequested)
        {
            return;
        }
        Trace.TraceWarning("Report presentation failed with {0}.",
            exception.GetType().Name);
        StatusMessage = Localize("ReportPlayer_Err_Unavailable");
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
