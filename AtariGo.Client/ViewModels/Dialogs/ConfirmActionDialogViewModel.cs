using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class ConfirmActionDialogViewModel : DialogViewModelBase
    {
        private string _message;
        private string _confirmButtonText;
        private string _cancelButtonText;

        public ConfirmActionDialogViewModel(
            string title,
            string message,
            string confirmButtonText = "Confirm")
            : base(title)
        {
            _message = message;
            _confirmButtonText = confirmButtonText;
            _cancelButtonText = "Cancel";

            ConfirmCommand = new RelayCommand(() => Close(true));
            CancelCommand = new RelayCommand(() => Close(false));
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string ConfirmButtonText
        {
            get => _confirmButtonText;
            set => SetProperty(ref _confirmButtonText, value);
        }

        public string CancelButtonText
        {
            get => _cancelButtonText;
            set => SetProperty(ref _cancelButtonText, value);
        }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }
    }
}
