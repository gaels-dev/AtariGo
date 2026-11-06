using System.Diagnostics;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels;

public sealed class GuestLobbyViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly Func<Task> _signOutAsync;

    public GuestLobbyViewModel(
        INavigationService navigationService,
        string playerName,
        Func<Task> signOutAsync)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        ArgumentNullException.ThrowIfNull(signOutAsync);

        _navigationService = navigationService;
        _signOutAsync = signOutAsync;
        PlayerName = playerName;
        PlayMultiplayerCommand = new RelayCommand(
            _navigationService.NavigateToLobby);
        OptionsCommand = new RelayCommand(
            _navigationService.OpenOptionsDialog);
        SignOutCommand = new AsyncRelayCommand(
            SignOutAsync,
            HandleFailure);
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

    private Task SignOutAsync(object? _)
    {
        return _signOutAsync();
    }

    private static void HandleFailure(Exception exception)
    {
        Trace.TraceError(
            "Guest sign-out failed with exception type {0}.",
            exception.GetType().Name);
    }

    private static string GetLocalizedMessage(string key)
    {
        return Resources.ResourceManager.GetString(
            key,
            System.Globalization.CultureInfo.CurrentUICulture)
            ?? key;
    }
}
