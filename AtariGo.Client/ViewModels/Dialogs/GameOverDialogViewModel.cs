using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class GameOverDialogViewModel : DialogViewModelBase
    {
        private string _message;
        private string _actionButtonText;

        public GameOverDialogViewModel(
            string title,
            string message,
            string actionButtonText)
            : base(title)
        {
            _message = message;
            _actionButtonText = actionButtonText;

            AcceptCommand = new RelayCommand(ExecuteAccept);
        }

        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                SetProperty(ref _message, value);
            }
        }

        public string ActionButtonText
        {
            get
            {
                return _actionButtonText;
            }
            set
            {
                SetProperty(ref _actionButtonText, value);
            }
        }

        public ICommand AcceptCommand
        {
            get;
        }

        private void ExecuteAccept()
        {
            Close(true);
        }
    }
}
