using System;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class VerificationDialogViewModel : ViewModelBase
    {
        private string _verificationCode = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly INavigationService _navigationService;
        private readonly string _username;

        public VerificationDialogViewModel(string username, INavigationService navigationService)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;

            ConfirmCommand = new RelayCommand(VerifyCode);
            CancelCommand = new RelayCommand(_navigationService.OpenRegisterDialog);
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
            _navigationService.NavigateToMainWindow(isGuest: false, playerName: _username);
        }
    }
}
