using System;
using System.Globalization;
using System.Net.Mail;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class RegisterDialogViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _passwordConfirmation = string.Empty;
    private string _errorMessage = string.Empty;
    private RegistrationDraft? _registrationDraft;

    public RegisterDialogViewModel(INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        _navigationService = navigationService;

        RegisterCommand = new RelayCommand(ProceedToVerification);
        CancelCommand = new RelayCommand(_navigationService.DiscardRegistration);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
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

    public string ConfirmPasswordLabel =>
        GetLocalizedMessage("Register_Lbl_ConfirmPassword");

    public ICommand RegisterCommand { get; }

    public ICommand CancelCommand { get; }

    public void ClearSensitiveData()
    {
        Password = string.Empty;
        PasswordConfirmation = string.Empty;
        _registrationDraft?.ClearSensitiveData();
        _registrationDraft = null;
    }

    private void ProceedToVerification()
    {
        string username = Username.Trim();
        string email = Email.Trim();

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(Password)
            || string.IsNullOrWhiteSpace(PasswordConfirmation))
        {
            ErrorMessage = GetLocalizedMessage("Register_Err_EmptyFields");
            return;
        }

        if (username.Length > 100 || email.Length > 254 || Password.Length > 255)
        {
            ErrorMessage = GetLocalizedMessage("Register_Err_Generic");
            return;
        }

        if (!MailAddress.TryCreate(email, out _))
        {
            ErrorMessage = GetLocalizedMessage("Register_Err_InvalidEmail");
            return;
        }

        if (Password.Length < 8 || !Password.ContainsUppercaseLetter())
        {
            ErrorMessage = GetLocalizedMessage("Register_Err_PasswordPolicy");
            return;
        }

        if (!string.Equals(Password, PasswordConfirmation, StringComparison.Ordinal))
        {
            ErrorMessage = GetLocalizedMessage("Register_Err_PasswordMismatch");
            return;
        }

        ErrorMessage = string.Empty;
        _registrationDraft = new RegistrationDraft
        {
            UserName = username,
            Email = email,
            Password = Password,
            PasswordConfirmation = PasswordConfirmation
        };

        _navigationService.OpenVerificationDialog(_registrationDraft);
    }

    private static string GetLocalizedMessage(string key) =>
        Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? key;
}

internal static class PasswordPolicyExtensions
{
    public static bool ContainsUppercaseLetter(this string value)
    {
        foreach (char character in value)
        {
            if (char.IsUpper(character))
            {
                return true;
            }
        }

        return false;
    }
}
