using System;
using System.Windows.Input;
using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class VerificationDialogViewModel : ViewModelBase
    {
        private string _verificationCode = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly Action _onBackRequested;
        private readonly Action<string> _onVerificationSuccess;
        private readonly string _username;

        public VerificationDialogViewModel(string username, Action onBackRequested, Action<string> onVerificationSuccess)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _onBackRequested = onBackRequested ?? throw new ArgumentNullException(nameof(onBackRequested));
            _onVerificationSuccess = onVerificationSuccess ?? throw new ArgumentNullException(nameof(onVerificationSuccess));

            ConfirmCommand = new RelayCommand(VerifyCode);
            CancelCommand = new RelayCommand(_onBackRequested);
        }

        public string VerificationCode
        {
            get => _verificationCode;
            set => SetProperty(ref _verificationCode, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        private void VerifyCode()
        {
            if (string.IsNullOrWhiteSpace(_verificationCode))
            {
                ErrorMessage = Properties.Resources.Verification_Err_EmptyCode;
                return;
            }

            ErrorMessage = string.Empty;
            _onVerificationSuccess(_username);
        }
    }
}