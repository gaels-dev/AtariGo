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
    private const int MaximumUserNameLength = 15;
    private const string ReservedGuestName = "Guest";
    private const string ReservedGuestPrefix = "Guest-";
    private const int MaximumEmailLength = 254;
    private const int MinimumPasswordLength = 8;
    private const int MaximumPasswordLength = 255;

    private readonly INavigationService _navigationService;
    private string _userName = string.Empty;
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
        string userName = UserName.Trim();
        string email = Email.Trim();

        string? validationError = GetFieldValidationError(userName, email)
            ?? GetIdentityValidationError(userName, email)
            ?? GetPasswordValidationError();
        if (validationError is not null)
        {
            ErrorMessage = GetLocalizedMessage(validationError);
            return;
        }

        ErrorMessage = string.Empty;
        _registrationDraft = new RegistrationDraft
        {
            UserName = userName,
            Email = email,
            Password = Password,
            PasswordConfirmation = PasswordConfirmation
        };

        _navigationService.OpenVerificationDialog(_registrationDraft);
    }

    private string? GetFieldValidationError(string userName, string email)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(Password)
            || string.IsNullOrWhiteSpace(PasswordConfirmation))
        {
            return "Register_Err_EmptyFields";
        }

        bool exceedsLimit = userName.Length > MaximumUserNameLength
            || email.Length > MaximumEmailLength || Password.Length > MaximumPasswordLength;
        return exceedsLimit ? "Register_Err_Generic" : null;
    }

    private static string? GetIdentityValidationError(string userName, string email)
    {
        if (IsReservedGuestName(userName))
        {
            return "Register_Err_ReservedUsername";
        }

        return MailAddress.TryCreate(email, out _) ? null : "Register_Err_InvalidEmail";
    }

    private string? GetPasswordValidationError()
    {
        if (Password.Length < MinimumPasswordLength || !Password.ContainsUppercaseLetter())
        {
            return "Register_Err_PasswordPolicy";
        }

        bool passwordsMatch = string.Equals(
            Password, PasswordConfirmation, StringComparison.Ordinal);
        return passwordsMatch ? null : "Register_Err_PasswordMismatch";
    }

    private static bool IsReservedGuestName(string userName)
    {
        return string.Equals(
                userName,
                ReservedGuestName,
                StringComparison.OrdinalIgnoreCase)
            || userName.StartsWith(
                ReservedGuestPrefix,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLocalizedMessage(string key) =>
        Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? key;
}
