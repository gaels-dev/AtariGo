using System;
using System.Windows.Input;
using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class ForgotPasswordDialogViewModel : ViewModelBase
    {
        private string _emailOrUsername = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly Action _onCloseRequested;
        private readonly Action<string> _onProceedToVerification;

        public ForgotPasswordDialogViewModel(Action onCloseRequested, Action<string> onProceedToVerification)
        {
            _onCloseRequested = onCloseRequested ?? throw new ArgumentNullException(nameof(onCloseRequested));
            _onProceedToVerification = onProceedToVerification ?? throw new ArgumentNullException(nameof(onProceedToVerification));

            AcceptCommand = new RelayCommand(ProceedToVerification);
            CancelCommand = new RelayCommand(_onCloseRequested);
        }

        public string EmailOrUsername
        {
            get => _emailOrUsername;
            set => SetProperty(ref _emailOrUsername, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand AcceptCommand { get; }

        public ICommand CancelCommand { get; }

        private void ProceedToVerification()
        {
            if (string.IsNullOrWhiteSpace(_emailOrUsername))
            {
                ErrorMessage = Properties.Resources.ForgotPassword_Err_Empty;
                return;
            }

            ErrorMessage = string.Empty;
            _onProceedToVerification(_emailOrUsername);
        }
    }
}