using System.Diagnostics;

using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.ViewModels;

public partial class GameBoardViewModel
{
    private TaskCompletionSource<bool>? _applicationExitConfirmation;
    private bool _isClosingApplication;

    public async Task<bool> RequestApplicationCloseAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return true;
        }
        if (_isLeaving || _isClosingApplication || _applicationExitConfirmation is not null)
        {
            return false;
        }
        if (GameId.HasValue && !_isFinished)
        {
            bool confirmed = await ConfirmApplicationExitAsync(cancellationToken);
            if (!confirmed)
            {
                return false;
            }
        }
        _isClosingApplication = true;
        IsChatEnabled = false;
        NotifyGameplayAvailability();
        OnPropertyChanged(nameof(IsGameplayInteractive));
        await FinishApplicationExitAsync(cancellationToken);
        return true;
    }

    private async Task FinishApplicationExitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CloseApplicationSessionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Trace.TraceError("Application game shutdown failed. Error: {0}.",
                exception.GetType().Name);
            _session?.Dispose();
        }
    }

    private Task CloseApplicationSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return Task.CompletedTask;
        }
        if (GameId.HasValue && !_isFinished)
        {
            return _session.SurrenderAndCloseAsync(cancellationToken);
        }
        return _session.CloseAsync(cancellationToken);
    }

    private async Task<bool> ConfirmApplicationExitAsync(CancellationToken cancellationToken)
    {
        ConfirmActionDialogViewModel dialog = new ConfirmActionDialogViewModel(
            GetLocalizedMessage("GameBoard_Exit_Title"),
            GetLocalizedMessage("GameBoard_Exit_Message"),
            GetLocalizedMessage("GameBoard_Exit_Confirm"));
        TaskCompletionSource<bool> confirmation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _applicationExitConfirmation = confirmation;
        dialog.DialogClosed += OnApplicationExitConfirmed;
        try
        {
            _navigationService.OpenDialog(dialog);
            return await confirmation.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            dialog.DialogClosed -= OnApplicationExitConfirmed;
            _applicationExitConfirmation = null;
        }
    }

    private void OnApplicationExitConfirmed(bool? confirmed)
    {
        _applicationExitConfirmation?.TrySetResult(confirmed == true);
    }

    private bool CompleteExitConfirmation()
    {
        if (_applicationExitConfirmation is null)
        {
            return false;
        }
        if (!_applicationExitConfirmation.TrySetResult(true))
        {
            return false;
        }
        _navigationService.CloseDialog();
        return true;
    }
}
