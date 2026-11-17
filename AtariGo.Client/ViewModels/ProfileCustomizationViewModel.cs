using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class ProfileCustomizationViewModel : ViewModelBase, IDisposable
{
    private const int DefaultAvatarId = 1;
    private const int MaximumAvatarId = 5;

    private readonly INavigationService _navigationService;
    private readonly IProfileClient _profileClient;
    private readonly Action<string> _onPlayerNameUpdated;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly CancellationToken _token;
    private string _playerName = string.Empty;
    private string _email = string.Empty;
    private string _statusMessage = string.Empty;
    private int _selectedAvatarId = DefaultAvatarId;
    private int _wins;
    private bool _isLoaded;
    private bool _isDisposed;

    public ProfileCustomizationViewModel(INavigationService navigationService,
        IProfileClient profileClient, Action<string> onPlayerNameUpdated)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(profileClient);
        ArgumentNullException.ThrowIfNull(onPlayerNameUpdated);
        _navigationService = navigationService;
        _profileClient = profileClient;
        _onPlayerNameUpdated = onPlayerNameUpdated;
        _token = _lifetime.Token;
        AvailableAvatars = Enumerable.Range(DefaultAvatarId, MaximumAvatarId).ToArray();
        LoadCommand = new AsyncRelayCommand(LoadProfileAsync, HandleFailure, _token);
        UpdateProfileCommand = new AsyncRelayCommand(UpdateProfileAsync, HandleFailure, _token);
        ChangePasswordCommand = new RelayCommand(ChangePassword);
        BackCommand = new RelayCommand(_navigationService.NavigateToMainMenu);
    }

    public IReadOnlyList<int> AvailableAvatars { get; }

    public string PlayerName
    {
        get => _playerName;
        set => SetProperty(ref _playerName, value);
    }

    public string Email
    {
        get => _email;
        private set => SetProperty(ref _email, value);
    }

    public int Wins
    {
        get => _wins;
        private set => SetProperty(ref _wins, value);
    }

    public string WinsLabel => Localize("ProfileCustomization_Lbl_Wins");

    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }

    public int SelectedAvatarId
    {
        get => _selectedAvatarId;
        set => SetProperty(ref _selectedAvatarId, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand LoadCommand { get; }

    public ICommand UpdateProfileCommand { get; }

    public ICommand ChangePasswordCommand { get; }

    public ICommand BackCommand { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task LoadProfileAsync(object? parameter)
    {
        try
        {
            UserProfile profile = await _profileClient.GetProfileAsync(_token);
            _token.ThrowIfCancellationRequested();
            ApplyProfile(profile);
            IsLoaded = true;
        }
        catch (RpcException exception)
        {
            HandleRpcFailure(exception);
        }
    }

    private async Task UpdateProfileAsync(object? parameter)
    {
        if (!IsLoaded)
        {
            return;
        }
        UpdateProfileRequest request = new UpdateProfileRequest
        {
            UserName = PlayerName,
            AvatarId = SelectedAvatarId
        };
        try
        {
            UpdateProfileResponse response = await _profileClient.UpdateProfileAsync(
                request, _token);
            _token.ThrowIfCancellationRequested();
            ApplyUpdateResponse(response);
        }
        catch (RpcException exception)
        {
            HandleRpcFailure(exception);
        }
    }

    private void ApplyUpdateResponse(UpdateProfileResponse response)
    {
        switch (response.Status)
        {
            case AtariGo.Contracts.ProfileUpdateStatus.Updated:
                ApplySuccessfulUpdate(response.Profile);
                break;
            case AtariGo.Contracts.ProfileUpdateStatus.InvalidUserName:
                StatusMessage = Localize("ProfileCustomization_Err_InvalidUserName");
                break;
            case AtariGo.Contracts.ProfileUpdateStatus.UserNameTaken:
                StatusMessage = Localize("ProfileCustomization_Err_UserNameTaken");
                break;
            case AtariGo.Contracts.ProfileUpdateStatus.InvalidAvatar:
                StatusMessage = Localize("ProfileCustomization_Err_InvalidAvatar");
                break;
            case AtariGo.Contracts.ProfileUpdateStatus.AccountUnavailable:
                StatusMessage = Localize("Connection_Err_SessionExpired");
                break;
            default:
                StatusMessage = Localize("ProfileCustomization_Err_Unavailable");
                break;
        }
    }

    private void ApplySuccessfulUpdate(UserProfile? profile)
    {
        if (profile is null)
        {
            StatusMessage = Localize("ProfileCustomization_Err_Unavailable");
            return;
        }
        ApplyProfile(profile);
        _onPlayerNameUpdated(profile.UserName);
        StatusMessage = Localize("ProfileCustomization_Msg_Updated");
    }

    private void ApplyProfile(UserProfile profile)
    {
        PlayerName = profile.UserName;
        Email = profile.Email;
        Wins = profile.Wins;
        SelectedAvatarId = profile.AvatarId;
    }

    private void HandleRpcFailure(RpcException exception)
    {
        if (_isDisposed || _token.IsCancellationRequested)
        {
            return;
        }
        Trace.TraceWarning("Profile request failed with status {0}.",
            exception.StatusCode);
        string key = exception.StatusCode == StatusCode.Unauthenticated
            ? "Connection_Err_SessionExpired" : "ProfileCustomization_Err_Unavailable";
        StatusMessage = Localize(key);
    }

    private void HandleFailure(Exception exception)
    {
        if (_isDisposed || _token.IsCancellationRequested)
        {
            return;
        }
        Trace.TraceWarning("Profile presentation failed with {0}.",
            exception.GetType().Name);
        StatusMessage = Localize("ProfileCustomization_Err_Unavailable");
    }

    private void ChangePassword()
    {
        _navigationService.OpenChangePasswordDialog();
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
