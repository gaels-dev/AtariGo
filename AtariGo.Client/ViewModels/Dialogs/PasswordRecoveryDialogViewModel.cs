using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class PasswordRecoveryDialogViewModel : ViewModelBase, IDisposable
{
    private readonly IPasswordRecoveryClient _client;
    private readonly INavigationService _navigation;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly CancellationToken _token;
    private PasswordRecoveryInput? _activeInput;
    private bool _isDisposed;
    private bool _isBusy;
    private bool _isResetStage;
    private bool _isFinished;
    private string _identifier = string.Empty;
    private string _code = string.Empty;
    private string _statusMessage = string.Empty;

    public PasswordRecoveryDialogViewModel(IPasswordRecoveryClient client,
        INavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(navigation);
        _client = client;
        _navigation = navigation;
        _token = _lifetime.Token;
        RequestCommand = new AsyncRelayCommand(RequestAsync, HandleFailure, _token);
        ResetCommand = new AsyncRelayCommand(ResetAsync, HandleFailure, _token);
        CancelCommand = new RelayCommand(Cancel);
    }

    public event Action? SecretsCleared;

    public string Identifier
    {
        get => _identifier;
        set
        {
            if (CanEditIdentifier)
            {
                SetProperty(ref _identifier, value);
            }
        }
    }

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsResetStage => _isResetStage;

    public bool CanEdit => !_isDisposed && !_isBusy && !_isFinished;

    public bool CanEditIdentifier => CanEdit && !IsResetStage;

    public bool CanReset => CanEdit && IsResetStage;

    public string Title => Localize("Recovery_Lbl_Title");

    public string IdentifierLabel => Localize("Recovery_Lbl_Identifier");

    public string CodeLabel => Localize("Recovery_Lbl_Code");

    public string NewPasswordLabel => Localize("ChangePassword_Lbl_NewPassword");

    public string ConfirmationLabel => Localize("ChangePassword_Lbl_Confirmation");

    public string PolicyLabel => Localize("ChangePassword_Lbl_Policy");

    public string RequestLabel => Localize(IsResetStage
        ? "Recovery_Btn_Resend" : "Recovery_Btn_Request");

    public string ResetLabel => Localize("Recovery_Btn_Reset");

    public string CancelLabel => Localize(_isFinished ? "ChangePassword_Btn_Close"
        : "ChangePassword_Btn_Cancel");

    public ICommand RequestCommand
    {
        get;
    }

    public ICommand ResetCommand
    {
        get;
    }

    public ICommand CancelCommand
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
        ClearSecrets();
        _identifier = string.Empty;
        SecretsCleared = null;
        _lifetime.Dispose();
    }

    private async Task RequestAsync(object? parameter)
    {
        if (!CanEdit)
        {
            return;
        }
        if (!PasswordRecoveryInput.IsIdentifierValid(Identifier))
        {
            ShowResult(PasswordRecoveryResult.MissingFields);
            return;
        }
        SetBusy(true);
        ClearSecrets();
        try
        {
            PasswordRecoveryResult result = await _client.RequestAsync(Identifier.Trim(), _token);
            _token.ThrowIfCancellationRequested();
            _isResetStage = result is PasswordRecoveryResult.Accepted
                or PasswordRecoveryResult.Unconfirmed || IsResetStage;
            ShowResult(result);
        }
        finally
        {
            if (!_isDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private async Task ResetAsync(object? parameter)
    {
        if (!CanReset || parameter is not PasswordRecoveryInput input)
        {
            return;
        }
        input.Identifier = Identifier.Trim();
        input.Code = Code;
        PasswordRecoveryResult? validation = input.Validate();
        if (validation.HasValue)
        {
            ShowResult(validation.Value);
            input.Clear();
            SecretsCleared?.Invoke();
            return;
        }
        _activeInput = input;
        SetBusy(true);
        StatusMessage = Localize("Recovery_Msg_Saving");
        try
        {
            PasswordRecoveryResult result = await _client.ResetAsync(input, _token);
            _token.ThrowIfCancellationRequested();
            _isFinished = result is PasswordRecoveryResult.Reset
                or PasswordRecoveryResult.Unconfirmed;
            ShowResult(result);
        }
        finally
        {
            input.Clear();
            if (!_isDisposed)
            {
                ClearSecrets();
                SetBusy(false);
            }
        }
    }

    private void Cancel()
    {
        Dispose();
        _navigation.CloseLoginDialog();
    }

    private void ClearSecrets()
    {
        _activeInput?.Clear();
        _activeInput = null;
        Code = string.Empty;
        SecretsCleared?.Invoke();
    }

    private void ShowResult(PasswordRecoveryResult result)
    {
        StatusMessage = Localize("Recovery_Result_" + result);
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanEditIdentifier));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(IsResetStage));
        OnPropertyChanged(nameof(RequestLabel));
        OnPropertyChanged(nameof(CancelLabel));
    }

    private void HandleFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            _isFinished = true;
            ClearSecrets();
            ShowResult(PasswordRecoveryResult.Unconfirmed);
            SetBusy(false);
        }
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
