using System.Reflection;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.Services
{
    public class RecordingNavigationProxy : DispatchProxy
    {
        private readonly List<string> _calls = new List<string>();

        public ClientSessionContext? SessionContext { get; private set; }

        public DialogViewModelBase? OpenedDialog { get; private set; }

        public IReadOnlyList<string> Calls
        {
            get
            {
                return _calls;
            }
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            _calls.Add(targetMethod.Name);
            if (targetMethod.Name == nameof(INavigationService.NavigateToMainWindow))
            {
                SessionContext = args?[0] as ClientSessionContext;
            }
            else if (targetMethod.Name == nameof(INavigationService.OpenDialog))
            {
                OpenedDialog = args?[0] as DialogViewModelBase;
            }
            return null;
        }
    }
}
