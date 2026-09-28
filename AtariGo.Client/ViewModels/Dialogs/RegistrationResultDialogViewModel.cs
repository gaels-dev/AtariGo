using System;
using System.Windows.Input;
using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs;

public sealed class RegistrationResultDialogViewModel : ViewModelBase
{
    private readonly Action _close;

    public RegistrationResultDialogViewModel(
        string message,
        string closeButtonText,
        Action close)
    {
        Message = message;
        CloseButtonText = closeButtonText;
        _close = close ?? throw new ArgumentNullException(nameof(close));
        CloseCommand = new RelayCommand(_close);
    }

    public string Message { get; }

    public string CloseButtonText { get; }

    public ICommand CloseCommand { get; }
}
