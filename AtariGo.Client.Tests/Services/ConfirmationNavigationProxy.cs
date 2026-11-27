using System.Reflection;

using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.Services;

public class ConfirmationNavigationProxy : DispatchProxy
{
    public bool Confirmed { get; set; } = true;

    public string Message { get; private set; } = string.Empty;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "OpenDialog"
            && args?[0] is ConfirmActionDialogViewModel dialog)
        {
            Message = dialog.Message;
            dialog.Close(Confirmed);
        }

        return null;
    }
}
