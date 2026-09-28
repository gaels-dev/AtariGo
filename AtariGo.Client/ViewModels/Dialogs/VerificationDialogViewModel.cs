using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;
using Grpc.Core;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class VerificationDialogViewModel : ViewModelBase
{
    private readonly RegistrationDraft _registrationDraft;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationClient _authenticationClient;
    private string _verificationCode = string.Empty;
    private string _errorMessage = string.Empty;

    public VerificationDialogViewModel(
        RegistrationDraft registrationDraft,
        INavigationService navigationService,
        IAuthenticationClient authenticationClient)
    {
        ArgumentNullException.ThrowIfNull(registrationDraft);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(authenticationClient);

        _registrationDraft = registrationDraft;
        _navigationService = navigationService;
        _authenticationClient = authenticationClient;
        ConfirmCommand = new AsyncRelayCommand(ConfirmRegistrationAsync);
        CancelCommand = new RelayCommand(_navigationService.ReturnToRegisterDialog);
    }

    public string VerificationCode
    {
        get => _verificationCode;
        set => SetProperty(ref _verificationCode, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    private async Task ConfirmRegistrationAsync(object? parameter)
    {
        string confirmationText = parameter as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(confirmationText))
        {
            ErrorMessage = GetLocalizedMessage("Verification_Err_EmptyCode");
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            RegisterAccountResponse response =
                await _authenticationClient.RegisterAccountAsync(
                    _registrationDraft.UserName,
                    _registrationDraft.Email,
                    _registrationDraft.Password,
                    _registrationDraft.PasswordConfirmation,
                    confirmationText);

            _navigationService.ShowRegistrationResult(response.Result);
        }
        catch (RpcException)
        {
            _navigationService.ShowRegistrationResult(RegistrationResult.Error);
        }
        catch (Exception)
        {
            _navigationService.ShowRegistrationResult(RegistrationResult.Error);
        }
    }

    private static string GetLocalizedMessage(string key) =>
        Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? key;
}
