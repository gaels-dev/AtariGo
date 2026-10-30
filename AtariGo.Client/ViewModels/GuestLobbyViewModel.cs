using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels;

public sealed class GuestLobbyViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public GuestLobbyViewModel(
        INavigationService navigationService,
        string playerName)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        _navigationService = navigationService;
        PlayerName = playerName;
        PlayMultiplayerCommand = new RelayCommand(
            _navigationService.NavigateToLobby);
        OptionsCommand = new RelayCommand(
            _navigationService.OpenOptionsDialog);
        SignOutCommand = new RelayCommand(
            _navigationService.SignOut);
    }

    public string PlayerName { get; }

    public string TitleText => GetLocalizedMessage(
        "GuestLobby_Title");

    public string PlayMultiplayerLabel => GetLocalizedMessage(
        "RegisterPlayerLobby_Btn_PlayMultiplayer");

    public string OptionsLabel => GetLocalizedMessage(
        "RegisterPlayerLobby_Btn_Options");

    public string SignOutLabel => GetLocalizedMessage(
        "RegisterPlayerLobby_Btn_SignOut");

    public ICommand PlayMultiplayerCommand { get; }

    public ICommand OptionsCommand { get; }

    public ICommand SignOutCommand { get; }

    private static string GetLocalizedMessage(string key)
    {
        return Resources.ResourceManager.GetString(
            key,
            System.Globalization.CultureInfo.CurrentUICulture)
            ?? key;
    }
}
