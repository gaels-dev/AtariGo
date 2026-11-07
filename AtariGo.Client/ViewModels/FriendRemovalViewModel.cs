using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class FriendRemovalViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly FriendListNavigationContext _context;
    private readonly IFriendshipsClient? _client;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private bool _isDisposed;
    private bool _isConfirming;
    private string _statusMessage = string.Empty;

    public FriendRemovalViewModel(
        INavigationService navigation,
        FriendListNavigationContext context,
        IFriendshipsClient? client = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Friends);
        _navigation = navigation;
        _context = context;
        _client = client ?? (string.IsNullOrWhiteSpace(context.SessionToken)
            ? null : new GrpcFriendshipsClient(context.SessionToken));
        _token = _lifetime.Token;
        RemoveCommand = new AsyncRelayCommand(RemoveAsync, HandleFailure, _token);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RemoveCommand { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetime.Cancel();
        if (_isConfirming)
        {
            _navigation.CloseDialog();
        }

        _lifetime.Dispose();
    }

    private async Task RemoveAsync(object? parameter)
    {
        if (_isDisposed || parameter is not FriendItemViewModel friend)
        {
            return;
        }

        if (_client is null)
        {
            throw new InvalidOperationException("An authenticated friendships client is required.");
        }

        if (!await ConfirmAsync(friend.Username))
        {
            return;
        }

        _token.ThrowIfCancellationRequested();
        StatusMessage = Localize("FriendList_Msg_Removing");
        RemoveFriendshipResponse response = await _client.RemoveFriendshipAsync(
            friend.UserId, _token);
        _token.ThrowIfCancellationRequested();
        if (response.Status != RemoveFriendshipStatus.Removed
            && response.Status != RemoveFriendshipStatus.AlreadyAbsent)
        {
            throw new InvalidOperationException("Unexpected friendship removal result.");
        }

        FriendItemViewModel? current = _context.Friends.FirstOrDefault(
            candidate => candidate.UserId == friend.UserId);
        if (current is not null)
        {
            _context.Friends.Remove(current);
        }

        StatusMessage = Localize("FriendList_Msg_Removed");
    }

    private async Task<bool> ConfirmAsync(string friendName)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new ConfirmActionDialogViewModel(
            Localize("FriendList_Lbl_RemoveTitle"),
            string.Format(CultureInfo.CurrentUICulture,
                Localize("FriendList_Msg_ConfirmRemove"), friendName));
        void HandleClosed(bool? confirmed)
        {
            completion.TrySetResult(confirmed == true);
        }

        dialog.DialogClosed += HandleClosed;
        using CancellationTokenRegistration registration = _token.Register(
            () => completion.TrySetCanceled(_token));
        _isConfirming = true;
        try
        {
            _navigation.OpenDialog(dialog);
            return await completion.Task;
        }
        finally
        {
            _isConfirming = false;
            dialog.DialogClosed -= HandleClosed;
        }
    }

    private void HandleFailure(Exception exception)
    {
        if (_isDisposed || GrpcCallPolicy.IsRequestedCancellation(exception, _token))
        {
            return;
        }

        _ = GrpcFailureMessages.GetMessage(exception);
        StatusMessage = Localize("FriendList_Err_Unconfirmed");
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
