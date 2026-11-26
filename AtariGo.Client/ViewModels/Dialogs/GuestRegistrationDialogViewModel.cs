using System.Diagnostics;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts.Validation;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class GuestRegistrationDialogViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly IGuestIdentityClient _guestIdentityClient;
    private readonly CancellationTokenSource _lifetimeCancellation =
        new CancellationTokenSource();
    private readonly CancellationToken _lifetimeToken;
    private bool _isDisposed;
    private bool _isBusy;
    private string _guestName = string.Empty;
    private string _errorMessage = string.Empty;

    public GuestRegistrationDialogViewModel(
        INavigationService navigationService,
        IGuestIdentityClient? guestIdentityClient = null)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        _navigationService = navigationService;
        _guestIdentityClient = guestIdentityClient ?? new GrpcMatchmakingClient();
        _lifetimeToken = _lifetimeCancellation.Token;

        EnterCommand = new AsyncRelayCommand(
            EnterAsGuestAsync,
            HandleFailure,
            _lifetimeToken);
        BackCommand = new RelayCommand(Cancel);
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

    public bool CanEdit => !_isDisposed && !_isBusy;

    public string TitleText => AuthenticationMessages.GetText(
        "GuestRegistration_Title");

    public string NameLabel => AuthenticationMessages.GetText(
        "GuestRegistration_Name");

    public string NamePlaceholder => AuthenticationMessages.GetText(
        "GuestRegistration_NamePlaceholder");

    public string EnterLabel => AuthenticationMessages.GetText(
        "GuestRegistration_Enter");

    public string BackLabel => AuthenticationMessages.GetText(
        "GuestRegistration_Back");

    public ICommand EnterCommand
    {
        get;
    }

    public ICommand BackCommand
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
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task EnterAsGuestAsync(object? parameter)
    {
        if (!CanEdit)
        {
            return;
        }
        string normalizedName = GuestName.Trim();
        if (normalizedName.Length == 0)
        {
            ErrorMessage = AuthenticationMessages.GetText(
                "GuestRegistration_Required");
            return;
        }

        if (!UserNameRules.IsValidGuestName(GuestName))
        {
            ErrorMessage = AuthenticationMessages.GetText(
                "GuestRegistration_Err_InvalidName");
            return;
        }

        _isBusy = true;
        OnPropertyChanged(nameof(CanEdit));
        ErrorMessage = string.Empty;
        try
        {
            string displayName = UserNameRules.GuestPrefix + normalizedName;
            string credential = await _guestIdentityClient.CreateAsync(displayName, _lifetimeToken);
            if (_lifetimeToken.IsCancellationRequested)
            {
                await RevokeLateIdentityAsync(credential);
                return;
            }
            if (string.IsNullOrWhiteSpace(credential))
            {
                throw new InvalidOperationException("The guest credential is missing.");
            }
            _navigationService.NavigateToMainWindow(new ClientSessionContext
            {
                IsGuest = true,
                PlayerName = displayName,
                GuestCredential = credential
            });
        }
        finally
        {
            _isBusy = false;
            if (!_isDisposed)
            {
                OnPropertyChanged(nameof(CanEdit));
            }
        }
    }

    private async Task RevokeLateIdentityAsync(string credential)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(GrpcCallPolicy.RequestTimeoutSeconds));
        try
        {
            await _guestIdentityClient.RevokeAsync(credential, timeout.Token);
        }
        catch (Exception exception) when (GrpcClientFailures.IsTransportFailure(exception))
        {
            Trace.TraceWarning("Late guest identity revocation failed: {0}.",
                exception.GetType().Name);
        }
    }

    private void HandleFailure(Exception _)
    {
        if (!_isDisposed)
        {
            ErrorMessage = AuthenticationMessages.GetText("GuestRegistration_Err_Connection");
        }
    }

    private void Cancel()
    {
        Dispose();
        _navigationService.CloseLoginDialog();
    }
}
