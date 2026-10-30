using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AtariGo.Client.Tests.Services
{
    public static class CommandExecution
    {
        private const int CommandCompletionSeconds = 5;

        public static Task ExecuteAsync(ICommand command, object? parameter)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler? handler = null;
            handler = (_, _) =>
            {
                if (command.CanExecute(parameter))
                {
                    command.CanExecuteChanged -= handler;
                    completion.TrySetResult();
                }
            };

            command.CanExecuteChanged += handler;
            command.Execute(parameter);
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(CommandCompletionSeconds));
        }
    }
}
