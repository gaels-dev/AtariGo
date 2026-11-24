using System.Diagnostics;
using System.Windows.Input;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Action<Exception> _onError;
    private readonly CancellationToken _cancellationToken;
    private bool _isExecuting;

    public AsyncRelayCommand(
        Func<object?, Task> execute,
        Action<Exception> onError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(onError);

        _execute = execute;
        _onError = onError;
        _cancellationToken = cancellationToken;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            await _execute(parameter);
        }
        catch (OperationCanceledException exception) when (
            _cancellationToken.IsCancellationRequested
            && exception.CancellationToken == _cancellationToken)
        {
            Trace.TraceInformation("Async command cancelled by its owner.");
        }
        catch (Exception exception)
        {
            try
            {
                _onError(exception);
            }
            catch (Exception handlerException)
            {
                Trace.TraceError(
                    "Async command failure handler failed. Exception types: {0}, {1}.",
                    DiagnosticError.FromException(handlerException),
                    DiagnosticError.FromException(exception));
            }
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
