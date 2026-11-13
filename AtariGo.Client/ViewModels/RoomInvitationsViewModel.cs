using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class RoomInvitationsViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly IRoomInvitationsClient _client;
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly ObservableCollection<FriendItemViewModel> _friends;
    private readonly HashSet<FriendItemViewModel> _observedFriends =
        new HashSet<FriendItemViewModel>();
    private readonly int _roomId;
    private RoomInvitationItemViewModel? _selectedInvitation;
    private Task? _refreshTask;
    private bool _refreshRequested;
    private bool _isBusy;
    private bool _isOpen;
    private bool _isDisposed;
    private bool _isAcceptanceAllowed;
    private bool _isRoomAvailable = true;
    private int _refreshGeneration;
    private string _message = string.Empty;

    public RoomInvitationsViewModel(
        INavigationService navigation,
        IRoomInvitationsClient client,
        RoomInvitationsNavigationContext context)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(context);
        _navigation = navigation;
        _client = client;
        _roomId = context.RoomId;
        _friends = context.Friends;
        _friends.CollectionChanged += OnFriendsChanged;
        TrackFriends();
        OpenCommand = new RelayCommand(Open);
        CloseCommand = new RelayCommand(Close);
        SendCommand = new AsyncRelayCommand(SendAsync, HandleMutationFailure, _lifetime.Token);
        AcceptCommand = new AsyncRelayCommand(AcceptAsync, HandleMutationFailure, _lifetime.Token);
        RejectCommand = new AsyncRelayCommand(RejectAsync, HandleMutationFailure, _lifetime.Token);
    }

    public ObservableCollection<FriendItemViewModel> OnlineFriends { get; } = new();

    public ObservableCollection<RoomInvitationItemViewModel> Invitations { get; } = new();

    public bool IsIncoming => _roomId == RoomInvitationsNavigationContext.IncomingRoomId;

    public bool IsOutgoing => !IsIncoming;

    public string Title => Text(IsIncoming ? "Title_Incoming" : "Title_Outgoing");

    public string OpenLabel => string.Format(CultureInfo.CurrentCulture,
        Text("Btn_Open"), Invitations.Count(invitation => invitation.IsPending));

    public string SendLabel => Text("Btn_Send");

    public string AcceptLabel => Text("Btn_Accept");

    public string RejectLabel => Text("Btn_Reject");

    public string CloseLabel => Text("Btn_Close");

    public string EmptyFriendsLabel => Text("Msg_NoOnlineFriends");

    public string EmptyInvitationsLabel => Text("Msg_NoInvitations");

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public bool IsAcceptanceAllowed
    {
        get => _isAcceptanceAllowed;
        set
        {
            if (SetProperty(ref _isAcceptanceAllowed, value))
            {
                NotifyState();
                if (!value && IsIncoming)
                {
                    IsOpen = false;
                }
            }
        }
    }

    public bool IsRoomAvailable
    {
        get => _isRoomAvailable;
        set
        {
            if (SetProperty(ref _isRoomAvailable, value))
            {
                NotifyState();
            }
        }
    }

    public RoomInvitationItemViewModel? SelectedInvitation
    {
        get => _selectedInvitation;
        set
        {
            SetProperty(ref _selectedInvitation, value);
            NotifyState();
        }
    }

    public bool CanInteract => !_isBusy && !_isDisposed;

    public bool CanOpen => CanInteract
        && (IsIncoming ? IsAcceptanceAllowed : IsRoomAvailable);

    public bool CanSend => CanInteract && IsOutgoing && IsRoomAvailable;

    public bool CanRespond => CanInteract && IsIncoming && IsAcceptanceAllowed
        && SelectedInvitation is { IsPending: true };

    public ICommand OpenCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand SendCommand { get; }

    public ICommand AcceptCommand { get; }

    public ICommand RejectCommand { get; }

    public void RequestRefresh()
    {
        if (_isDisposed || IsOutgoing)
        {
            return;
        }

        _refreshRequested = true;
        if (_refreshTask is null || _refreshTask.IsCompleted)
        {
            _refreshTask = RefreshQueuedAsync();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _friends.CollectionChanged -= OnFriendsChanged;
        foreach (FriendItemViewModel friend in _observedFriends)
        {
            friend.PropertyChanged -= OnFriendPresenceChanged;
        }

        _observedFriends.Clear();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void Open()
    {
        if (CanOpen)
        {
            IsOpen = true;
            Message = string.Empty;
            RequestRefresh();
        }
    }

    private void Close()
    {
        if (CanInteract)
        {
            IsOpen = false;
        }
    }

    private async Task RefreshQueuedAsync()
    {
        while (_refreshRequested && !_isDisposed)
        {
            _refreshRequested = false;
            try
            {
                await LoadInvitationsAsync();
            }
            catch (Exception exception) when (!_isDisposed)
            {
                ShowFailure(exception);
            }
            catch (Exception) when (_isDisposed)
            {
                Trace.TraceInformation("Invitation refresh stopped with its owner.");
            }
        }
    }

    private async Task LoadInvitationsAsync()
    {
        int generation = ++_refreshGeneration;
        ListRoomInvitationsResponse response = await _client.ListAsync(_roomId, _lifetime.Token);
        if (_isDisposed || generation != _refreshGeneration)
        {
            return;
        }

        long? selectedId = SelectedInvitation?.Invitation.InvitationId;
        Invitations.Clear();
        foreach (RoomInvitation invitation in response.Invitations)
        {
            Invitations.Add(new RoomInvitationItemViewModel(invitation));
        }

        SelectedInvitation = Invitations.FirstOrDefault(
            item => item.Invitation.InvitationId == selectedId);
        OnPropertyChanged(nameof(OpenLabel));
        NotifyState();
    }

    private async Task SendAsync(object? parameter)
    {
        if (parameter is not FriendItemViewModel friend || !CanSend
            || !OnlineFriends.Contains(friend))
        {
            return;
        }

        var request = new SendRoomInvitationRequest
        {
            RoomId = _roomId, RecipientUserId = friend.UserId
        };
        SetBusy(true);
        try
        {
            await _client.SendAsync(request, _lifetime.Token);
            if (!_isDisposed)
            {
                Message = Text("Msg_Sent");
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private Task AcceptAsync(object? parameter)
    {
        return RespondAsync(true);
    }

    private Task RejectAsync(object? parameter)
    {
        return RespondAsync(false);
    }

    private async Task RespondAsync(bool accept)
    {
        if (!CanRespond)
        {
            return;
        }

        var request = new RespondRoomInvitationRequest
        {
            InvitationId = SelectedInvitation!.Invitation.InvitationId, Accept = accept
        };
        SetBusy(true);
        try
        {
            RespondRoomInvitationResponse response = await _client.RespondAsync(
                request, _lifetime.Token);
            if (!_isDisposed)
            {
                ApplyResponse(response, accept);
                await LoadInvitationsAsync();
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyResponse(RespondRoomInvitationResponse response, bool accepted)
    {
        if (accepted && response.Room is not null && IsAcceptanceAllowed)
        {
            IsOpen = false;
            _navigation.NavigateToPrivateRoomLobby(new PrivateRoomNavigationContext(
                response.Room, IsHost: false));
        }
        else
        {
            Message = Text("Msg_Updated");
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (!_isDisposed)
        {
            NotifyState();
        }
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanRespond));
    }

    private void OnFriendsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        TrackFriends();
    }

    private void TrackFriends()
    {
        foreach (FriendItemViewModel friend in _observedFriends)
        {
            friend.PropertyChanged -= OnFriendPresenceChanged;
        }

        _observedFriends.Clear();
        foreach (FriendItemViewModel friend in _friends)
        {
            if (_observedFriends.Add(friend))
            {
                friend.PropertyChanged += OnFriendPresenceChanged;
            }
        }

        UpdateOnlineFriends();
    }

    private void OnFriendPresenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FriendItemViewModel.IsOnline))
        {
            UpdateOnlineFriends();
        }
    }

    private void UpdateOnlineFriends()
    {
        OnlineFriends.Clear();
        foreach (FriendItemViewModel friend in _friends.Where(friend => friend.IsOnline))
        {
            OnlineFriends.Add(friend);
        }
    }

    private void HandleMutationFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            Trace.TraceWarning("Invitation mutation unconfirmed: {0}.", exception.GetType().Name);
            Message = Text(GetMutationFailureKey(exception));
            RequestRefresh();
        }
    }

    private static string GetMutationFailureKey(Exception exception)
    {
        if (exception is not RpcException failure)
        {
            return "Err_Unconfirmed";
        }

        return failure.Status.Detail switch
        {
            "NotFriends" => "Err_NotFriends",
            "RecipientOffline" => "Err_RecipientOffline",
            "RoomUnavailable" => "Err_RoomUnavailable",
            "InvitationUnavailable" => "Err_InvitationUnavailable",
            "AlreadyInRoom" => "Err_AlreadyInRoom",
            "PermissionDenied" => "Err_PermissionDenied",
            "NotFound" => "Err_NotFound",
            _ => "Err_Unconfirmed"
        };
    }

    private void ShowFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            Message = GrpcFailureMessages.GetMessage(exception);
        }
    }

    private static string Text(string key)
    {
        string resourceKey = "RoomInvitations_" + key;
        return Properties.Resources.ResourceManager.GetString(
            resourceKey, CultureInfo.CurrentUICulture) ?? resourceKey;
    }
}
