using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class GuestRegistrationDialogViewModel : ViewModelBase
{
    private const int MaximumGuestNameLength = 15;
    private const string GuestNamePrefix = "Guest-";

    private readonly INavigationService _navigationService;
    private string _guestName = string.Empty;
    private string _errorMessage = string.Empty;

    public GuestRegistrationDialogViewModel(
        INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        _navigationService = navigationService;

        EnterCommand = new RelayCommand(EnterAsGuest);
        BackCommand = new RelayCommand(
            _navigationService.CloseLoginDialog);
    }

    public string GuestName
    {
        get => _guestName;
        set => SetProperty(ref _guestName, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string TitleText => GetLocalizedMessage(
        "GuestRegistration_Title");

    public string NameLabel => GetLocalizedMessage(
        "GuestRegistration_Name");

    public string NamePlaceholder => GetLocalizedMessage(
        "GuestRegistration_NamePlaceholder");

    public string EnterLabel => GetLocalizedMessage(
        "GuestRegistration_Enter");

    public string BackLabel => GetLocalizedMessage(
        "GuestRegistration_Back");

    public ICommand EnterCommand { get; }

    public ICommand BackCommand { get; }

    private void EnterAsGuest()
    {
        string normalizedName = GuestName.Trim();
        if (normalizedName.Length == 0)
        {
            ErrorMessage = GetLocalizedMessage(
                "GuestRegistration_Required");
            return;
        }

        if (normalizedName.Length > MaximumGuestNameLength)
        {
            ErrorMessage = GetLocalizedMessage(
                "GuestRegistration_NameTooLong");
            return;
        }

        _navigationService.NavigateToMainWindow(
            isGuest: true,
            $"{GuestNamePrefix}{normalizedName}");
    }

    private static string GetLocalizedMessage(string key)
    {
        return Resources.ResourceManager.GetString(
            key,
            CultureInfo.CurrentUICulture)
            ?? key;
    }
}
