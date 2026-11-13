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
            string confirmButtonText = "")
            : base(title)
        {
            _message = message;
            _confirmButtonText = string.IsNullOrEmpty(confirmButtonText)
                ? Properties.Resources.ConfirmAction_Btn_Confirm
                : confirmButtonText;
            _cancelButtonText = Properties.Resources.ConfirmAction_Btn_Cancel;

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
