using System;
using System.Windows.Input;
using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class RegisterDialogViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly Action _onCloseRequested;
        private readonly Action<string, string> _onProceedToVerification;

        public RegisterDialogViewModel(Action onCloseRequested, Action<string, string> onProceedToVerification)
        {
            _onCloseRequested = onCloseRequested ?? throw new ArgumentNullException(nameof(onCloseRequested));
            _onProceedToVerification = onProceedToVerification ?? throw new ArgumentNullException(nameof(onProceedToVerification));

            RegisterCommand = new RelayCommand(ProceedToVerification);
            CancelCommand = new RelayCommand(_onCloseRequested);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand RegisterCommand { get; }

        public ICommand CancelCommand { get; }

        private void ProceedToVerification()
        {
            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_email))
            {
                ErrorMessage = Properties.Resources.Register_Err_EmptyFields;
                return;
            }

            ErrorMessage = string.Empty;
            _onProceedToVerification(_username, _email);
        }
    }
}