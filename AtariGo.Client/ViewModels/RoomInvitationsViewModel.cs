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
    private readonly CancellationTokenSource _lifetime = new();
    private readonly int _roomId;
    private FriendItemViewModel? _selectedFriend;
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
        RoomCode = context.RoomCode;
        Friends = context.Friends;
        Friends.CollectionChanged += OnFriendsChanged;
        OpenCommand = new RelayCommand(Open);
        CloseCommand = new RelayCommand(Close);
        SendCommand = new AsyncRelayCommand(SendAsync, HandleMutationFailure, _lifetime.Token);
        AcceptCommand = new AsyncRelayCommand(AcceptAsync, HandleMutationFailure, _lifetime.Token);
        RejectCommand = new AsyncRelayCommand(RejectAsync, HandleMutationFailure, _lifetime.Token);
        CancelCommand = new AsyncRelayCommand(CancelAsync, HandleMutationFailure, _lifetime.Token);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), ShowFailure, _lifetime.Token);
    }

    public ObservableCollection<FriendItemViewModel> Friends { get; }

    public ObservableCollection<RoomInvitationItemViewModel> Invitations { get; } = new();

    public bool IsIncoming => _roomId == 0;

    public bool IsOutgoing => !IsIncoming;

    public string RoomCode { get; }

    public string Title => Text(IsIncoming ? "Title_Incoming" : "Title_Outgoing");

    public string OpenLabel => string.Format(CultureInfo.CurrentCulture,
        Text("Btn_Open"), Invitations.Count(invitation => invitation.IsPending));

    public string SendLabel => Text("Btn_Send");

    public string AcceptLabel => Text("Btn_Accept");

    public string RejectLabel => Text("Btn_Reject");

    public string CancelLabel => Text("Btn_Cancel");

    public string CloseLabel => Text("Btn_Close");

    public string RefreshLabel => Text("Btn_Refresh");

    public string FriendLabel => Text("Lbl_Friend");

    public string RoomLabel => Text("Lbl_Room");

    public string SelectedRecipientLabel => string.Format(CultureInfo.CurrentCulture,
        Text("Lbl_Review"), SelectedFriend?.Username ?? "—", RoomCode);

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

    public FriendItemViewModel? SelectedFriend
    {
        get => _selectedFriend;
        set
        {
            if (_selectedFriend is not null)
            {
                _selectedFriend.PropertyChanged -= OnSelectedFriendChanged;
            }

            SetProperty(ref _selectedFriend, value);
            if (_selectedFriend is not null)
            {
                _selectedFriend.PropertyChanged += OnSelectedFriendChanged;
            }

            OnPropertyChanged(nameof(SelectedRecipientLabel));
            NotifyState();
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

    public bool CanOpen => CanInteract && (!IsIncoming || IsAcceptanceAllowed);

    public bool CanSend => CanInteract && IsOutgoing && IsRoomAvailable
        && SelectedFriend is { IsOnline: true }
        && Friends.Contains(SelectedFriend);

    public bool CanRespond => CanInteract && IsIncoming && IsAcceptanceAllowed
        && SelectedInvitation is { IsPending: true };

    public bool CanCancel => CanInteract && IsOutgoing && SelectedInvitation is { IsPending: true };

    public ICommand OpenCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand SendCommand { get; }

    public ICommand AcceptCommand { get; }

    public ICommand RejectCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand RefreshCommand { get; }

    public void RequestRefresh()
    {
        if (_isDisposed)
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
        Friends.CollectionChanged -= OnFriendsChanged;
        if (_selectedFriend is not null)
        {
            _selectedFriend.PropertyChanged -= OnSelectedFriendChanged;
        }

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
        IsOpen = false;
        SelectedFriend = null;
    }

    private async Task RefreshQueuedAsync()
    {
        while (_refreshRequested && !_isDisposed)
        {
            _refreshRequested = false;
            try
            {
                await RefreshAsync();
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

    private async Task RefreshAsync()
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
            Invitations.Add(new RoomInvitationItemViewModel(invitation, IsIncoming));
        }

        SelectedInvitation = Invitations.FirstOrDefault(
            item => item.Invitation.InvitationId == selectedId);
        OnPropertyChanged(nameof(OpenLabel));
        NotifyState();
    }

    private async Task SendAsync(object? parameter)
    {
        if (!CanSend)
        {
            return;
        }

        var request = new SendRoomInvitationRequest
        {
            RoomId = _roomId, RecipientUserId = SelectedFriend!.UserId
        };
        SetBusy(true);
        try
        {
            await _client.SendAsync(request, _lifetime.Token);
            if (!_isDisposed)
            {
                Message = Text("Msg_Sent");
                await RefreshAsync();
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
                await RefreshAsync();
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
            Close();
            _navigation.NavigateToPrivateRoomLobby(new PrivateRoomNavigationContext(
                response.Room, IsHost: false));
        }
        else
        {
            Message = Text("Msg_Updated");
        }
    }

    private async Task CancelAsync(object? parameter)
    {
        if (!CanCancel)
        {
            return;
        }

        long invitationId = SelectedInvitation!.Invitation.InvitationId;
        SetBusy(true);
        try
        {
            await _client.CancelAsync(invitationId, _lifetime.Token);
            if (!_isDisposed)
            {
                Message = Text("Msg_Updated");
                await RefreshAsync();
            }
        }
        finally
        {
            SetBusy(false);
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
        OnPropertyChanged(nameof(CanCancel));
    }

    private void OnFriendsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (SelectedFriend is not null && !Friends.Contains(SelectedFriend))
        {
            SelectedFriend = null;
        }

        NotifyState();
    }

    private void OnSelectedFriendChanged(object? sender, PropertyChangedEventArgs args)
    {
        NotifyState();
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
