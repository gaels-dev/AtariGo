using System.Diagnostics;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;
using AtariGo.Contracts.Validation;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class RegisterDialogViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly IRegistrationClient _registrationClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private string _userName = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _passwordConfirmation = string.Empty;
    private string _errorMessage = string.Empty;
    private RegistrationDraft? _activeDraft;
    private bool _isDisposed;
    private bool _isBusy;

    public RegisterDialogViewModel(INavigationService navigationService,
        IRegistrationClient? registrationClient = null)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        _navigationService = navigationService;
        _registrationClient = registrationClient ?? new GrpcRegistrationClient();
        _lifetimeToken = _lifetimeCancellation.Token;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, HandleFailure, _lifetimeToken);
        CancelCommand = new RelayCommand(Cancel);
    }

    public event Action? SecretsCleared;

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string PasswordConfirmation
    {
        get => _passwordConfirmation;
        set => SetProperty(ref _passwordConfirmation, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool CanEdit => !_isDisposed && !_isBusy;
    public string ConfirmPasswordLabel => AuthenticationMessages.GetText(
        "Register_Lbl_ConfirmPassword");
    public string PolicyLabel => AuthenticationMessages.GetPasswordPolicy();
    public ICommand RegisterCommand { get; }
    public ICommand CancelCommand { get; }

    public void ClearSensitiveData()
    {
        Password = string.Empty;
        PasswordConfirmation = string.Empty;
        _activeDraft?.ClearSensitiveData();
        _activeDraft = null;
        SecretsCleared?.Invoke();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        _lifetimeCancellation.Cancel();
        ClearSensitiveData();
        _lifetimeCancellation.Dispose();
    }

    private async Task RegisterAsync(object? parameter)
    {
        if (!CanEdit)
        {
            return;
        }
        string? error = GetIdentityError() ?? GetPasswordError();
        if (error is not null)
        {
            ErrorMessage = error == "Register_Err_PasswordPolicy"
                ? PolicyLabel : AuthenticationMessages.GetText(error);
            return;
        }
        _activeDraft = new RegistrationDraft
        {
            UserName = UserName.Trim(),
            Email = Email.Trim(),
            Password = Password,
            PasswordConfirmation = PasswordConfirmation
        };
        SetBusy(true);
        ErrorMessage = AuthenticationMessages.GetText("Registration_Msg_Saving");
        try
        {
            RegisterAccountResponse response = await _registrationClient.RegisterAccountAsync(
                _activeDraft, _lifetimeToken);
            _lifetimeToken.ThrowIfCancellationRequested();
            ApplyResult(response.Result);
        }
        catch (Exception exception)
            when (GrpcCallPolicy.IsRequestedCancellation(exception, _lifetimeToken))
        {
            Trace.TraceInformation("Registration wait cancelled because its dialog was closed.");
        }
        finally
        {
            if (!_isDisposed)
            {
                ClearSensitiveData();
                SetBusy(false);
            }
        }
    }

    private string? GetIdentityError()
    {
        string userName = UserName.Trim();
        if (userName.Contains('@'))
        {
            return "Register_Err_UsernameAtSign";
        }
        if (UserNameRules.IsReserved(userName))
        {
            return "Register_Err_ReservedUsername";
        }
        if (!UserNameRules.IsValid(UserName))
        {
            return "Register_Err_InvalidUsername";
        }
        return EmailAddressRules.IsValid(Email) ? null : "Register_Err_InvalidEmail";
    }

    private string? GetPasswordError()
    {
        if (!PasswordPolicy.IsValid(Password))
        {
            return "Register_Err_PasswordPolicy";
        }
        return string.Equals(Password, PasswordConfirmation, StringComparison.Ordinal)
            ? null : "Register_Err_PasswordMismatch";
    }

    private void ApplyResult(RegistrationResult result)
    {
        if (result == RegistrationResult.Success)
        {
            _navigationService.ShowRegistrationSuccess();
            return;
        }
        string key = result switch
        {
            RegistrationResult.UsernameTaken => "RegistrationResult_Msg_UsernameTaken",
            RegistrationResult.EmailTaken => "RegistrationResult_Msg_EmailTaken",
            _ => "RegistrationResult_Msg_Error"
        };
        ErrorMessage = AuthenticationMessages.GetText(key);
    }

    private void HandleFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            ErrorMessage = ClientFailureMessages.GetRegistrationMessage(exception);
        }
    }

    private void Cancel()
    {
        Dispose();
        _navigationService.CloseLoginDialog();
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        OnPropertyChanged(nameof(CanEdit));
    }
}
