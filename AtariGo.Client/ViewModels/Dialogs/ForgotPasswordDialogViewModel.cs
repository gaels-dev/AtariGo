using System;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class ForgotPasswordDialogViewModel : ViewModelBase
    {
        private string _emailOrUsername = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly INavigationService _navigationService;

        public ForgotPasswordDialogViewModel(INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;

            AcceptCommand = new RelayCommand(ProceedToVerification);
            CancelCommand = new RelayCommand(_navigationService.CloseLoginDialog);
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
            _navigationService.OpenForgotVerificationDialog();
        }
    }
}
