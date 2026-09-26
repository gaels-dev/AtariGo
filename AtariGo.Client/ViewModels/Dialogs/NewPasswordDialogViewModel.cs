using System;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class NewPasswordDialogViewModel : ViewModelBase
    {
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly INavigationService _navigationService;

        public NewPasswordDialogViewModel(INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;

            ConfirmChangeCommand = new RelayCommand(ConfirmChange);
            CancelCommand = new RelayCommand(_navigationService.CloseLoginDialog);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ConfirmChangeCommand { get; }

        public ICommand CancelCommand { get; }

        private void ConfirmChange()
        {
            if (string.IsNullOrWhiteSpace(_newPassword)
                || string.IsNullOrWhiteSpace(_confirmPassword))
            {
                ErrorMessage = Properties.Resources.NewPassword_Err_Empty;
                return;
            }

            if (_newPassword != _confirmPassword)
            {
                ErrorMessage = Properties.Resources.NewPassword_Err_Mismatch;
                return;
            }

            ErrorMessage = string.Empty;
            _navigationService.CloseLoginDialog();
        }
    }
}
