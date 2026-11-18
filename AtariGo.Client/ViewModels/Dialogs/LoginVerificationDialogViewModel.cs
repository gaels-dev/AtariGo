using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class LoginVerificationDialogViewModel : ViewModelBase, IDisposable
{
    private const int CodeLength = 8;
    private readonly IAuthenticationClient _client;
    private readonly Action<LoginAttemptResult> _complete;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly CancellationToken _token;
    private string _challengeToken;
    private string _code = string.Empty;
    private string _message = string.Empty;
    private string _destination = string.Empty;
    private string _expiresAt = string.Empty;
    private bool _isDisposed;
    private bool _isBusy;
    private bool _isFinished;

    public LoginVerificationDialogViewModel(IAuthenticationClient client,
        LoginAttemptResult pending, Action<LoginAttemptResult> complete)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(complete);
        _client = client;
        _token = _lifetime.Token;
        _complete = complete;
        _challengeToken = pending.ChallengeToken;
        ApplyResult(pending);
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync, HandleFailure, _token);
        ResendCommand = new AsyncRelayCommand(ResendAsync, HandleFailure, _token);
        CancelCommand = new AsyncRelayCommand(CancelAsync, HandleFailure, _token);
    }

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string Destination => _destination;

    public string ExpiresAt => _expiresAt;

    public bool CanEdit => !_isDisposed && !_isBusy && !_isFinished;

    public string Title => LoginAttemptMessages.GetText("LoginVerification_Lbl_Title");

    public string CodeLabel => LoginAttemptMessages.GetText("LoginVerification_Lbl_Code");

    public string ConfirmLabel => LoginAttemptMessages.GetText("LoginVerification_Btn_Confirm");

    public string ResendLabel => LoginAttemptMessages.GetText("LoginVerification_Btn_Resend");

    public string CancelLabel => LoginAttemptMessages.GetText("LoginVerification_Btn_Cancel");

    public ICommand ConfirmCommand { get; }

    public ICommand ResendCommand { get; }

    public ICommand CancelCommand { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        _lifetime.Cancel();
        _challengeToken = string.Empty;
        Code = string.Empty;
        _lifetime.Dispose();
    }

    private async Task ConfirmAsync(object? parameter)
    {
        if (!CanEdit)
        {
            return;
        }
        if (Code.Length != CodeLength
            || Code.Any(character => character is < '0' or > '9'))
        {
            Message = LoginAttemptMessages.GetText("LoginVerification_Result_InvalidCode");
            return;
        }
        SetBusy(true);
        try
        {
            LoginAttemptResult result = await _client.VerifyLoginCodeAsync(_challengeToken, Code,
                _token);
            _token.ThrowIfCancellationRequested();
            ApplyResult(result, true);
            if (result.Status == LoginAttemptStatus.Success)
            {
                _challengeToken = string.Empty;
                _complete(result);
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                Code = string.Empty;
                SetBusy(false);
            }
        }
    }

    private async Task ResendAsync(object? parameter)
    {
        if (!CanEdit)
        {
            return;
        }
        SetBusy(true);
        try
        {
            LoginAttemptResult result = await _client.ResendLoginCodeAsync(_challengeToken,
                _token);
            _token.ThrowIfCancellationRequested();
            ApplyResult(result);
            Code = string.Empty;
        }
        finally
        {
            if (!_isDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private async Task CancelAsync(object? parameter)
    {
        string token = _challengeToken;
        Dispose();
        _complete(new LoginAttemptResult { Status = LoginAttemptStatus.InvalidChallenge });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(
            GrpcCallPolicy.RequestTimeoutSeconds));
        await _client.CancelLoginAsync(token, timeout.Token);
    }

    private void ApplyResult(LoginAttemptResult result, bool isConfirmation = false)
    {
        Message = LoginAttemptMessages.GetMessage(result);
        _isFinished = result.Status is LoginAttemptStatus.Success
            or LoginAttemptStatus.InvalidChallenge or LoginAttemptStatus.TemporarilyLocked
            or LoginAttemptStatus.AccountRestricted
            || (result.Status == LoginAttemptStatus.Unconfirmed && isConfirmation)
            || (result.Status == LoginAttemptStatus.DeliveryFailed
                && string.IsNullOrEmpty(result.ChallengeToken));
        if (result.CodeExpiresAtUnixSeconds > 0)
        {
            _destination = result.MaskedEmail;
            DateTimeOffset expiry = DateTimeOffset.FromUnixTimeSeconds(
                result.CodeExpiresAtUnixSeconds).ToLocalTime();
            _expiresAt = string.Format(CultureInfo.CurrentUICulture,
                LoginAttemptMessages.GetText("LoginVerification_Msg_ExpiresAt"), expiry);
        }
        OnPropertyChanged(nameof(Destination));
        OnPropertyChanged(nameof(ExpiresAt));
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        OnPropertyChanged(nameof(CanEdit));
    }

    private void HandleFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            _isFinished = true;
            Code = string.Empty;
            Message = LoginAttemptMessages.GetText("LoginVerification_Result_Unconfirmed");
            SetBusy(false);
        }
    }
}
