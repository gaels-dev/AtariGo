using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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

    /// <summary>
    /// Runs the task at the ICommand void boundary and handles command failures.
    /// </summary>
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
        catch (Exception) when (_cancellationToken.IsCancellationRequested)
        {
            Trace.TraceInformation("Async command cancelled by its owner.");
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Async command failed. Exception type: {0}.",
                exception.GetType().Name);

            try
            {
                _onError(exception);
            }
            catch (Exception handlerException)
            {
                Trace.TraceError(
                    "Async command failure handler failed. Exception types: {0}, {1}.",
                    exception.GetType().Name,
                    handlerException.GetType().Name);
            }
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
