using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class ChangePasswordDialogViewModel : DialogViewModelBase, IDisposable
{
    private readonly ICredentialsClient _client;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly CancellationToken _token;
    private PasswordChangeInput? _activeInput;
    private bool _isSaving;
    private bool _hasSucceeded;
    private bool _isDisposed;
    private string _statusMessage = string.Empty;

    public ChangePasswordDialogViewModel(ICredentialsClient client)
        : base(Localize("ChangePassword_Lbl_Title"))
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _token = _lifetime.Token;
        SaveCommand = new AsyncRelayCommand(ChangePasswordAsync, HandleFailure, _token);
        DismissCommand = new RelayCommand(Dismiss);
    }

    public event Action? PasswordsCleared;

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            SetProperty(ref _isSaving, value);
            NotifyAvailability();
        }
    }

    public bool HasSucceeded
    {
        get => _hasSucceeded;
        private set
        {
            SetProperty(ref _hasSucceeded, value);
            NotifyAvailability();
        }
    }

    public bool CanEdit => !IsSaving && !HasSucceeded && !_isDisposed;

    public bool CanDismiss => !IsSaving && !_isDisposed;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentPasswordLabel => Localize("ChangePassword_Lbl_CurrentPassword");

    public string NewPasswordLabel => Localize("ChangePassword_Lbl_NewPassword");

    public string ConfirmationLabel => Localize("ChangePassword_Lbl_Confirmation");

    public string PolicyLabel => AuthenticationMessages.GetPasswordPolicy();

    public string SaveLabel => Localize("ChangePassword_Btn_Save");

    public string DismissLabel => HasSucceeded
        ? Localize("ChangePassword_Btn_Close") : Localize("ChangePassword_Btn_Cancel");

    public ICommand SaveCommand
    {
        get;
    }

    public ICommand DismissCommand
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
        ClearPasswords();
        PasswordsCleared = null;
        _lifetime.Dispose();
    }

    private async Task ChangePasswordAsync(object? parameter)
    {
        if (!CanEdit || parameter is not PasswordChangeInput input)
        {
            return;
        }
        PasswordChangeResult? validation = input.Validate();
        if (validation.HasValue)
        {
            ShowResult(validation.Value);
            input.Clear();
            return;
        }
        _activeInput = input;
        IsSaving = true;
        StatusMessage = Localize("ChangePassword_Msg_Saving");
        try
        {
            PasswordChangeResult result = await _client.ChangePasswordAsync(input, _token);
            _token.ThrowIfCancellationRequested();
            ShowResult(result);
        }
        finally
        {
            input.Clear();
            if (!_isDisposed)
            {
                ClearPasswords();
                IsSaving = false;
            }
        }
    }

    private void ShowResult(PasswordChangeResult result)
    {
        HasSucceeded = result == PasswordChangeResult.Changed;
        if (result == PasswordChangeResult.InvalidNewPassword)
        {
            StatusMessage = PolicyLabel;
            return;
        }
        StatusMessage = Localize("ChangePassword_Result_" + result);
    }

    private void Dismiss()
    {
        if (!CanDismiss)
        {
            return;
        }
        ClearPasswords();
        Close(HasSucceeded);
    }

    private void ClearPasswords()
    {
        _activeInput?.Clear();
        _activeInput = null;
        PasswordsCleared?.Invoke();
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanDismiss));
        OnPropertyChanged(nameof(DismissLabel));
    }

    private void HandleFailure(Exception exception)
    {
        if (_isDisposed || _token.IsCancellationRequested)
        {
            return;
        }
        Trace.TraceWarning("Password change was not confirmed. Error type: {0}.",
            exception.GetType().Name);
        ShowResult(PasswordChangeResult.Unconfirmed);
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
