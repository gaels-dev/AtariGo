using System;
using System.Windows.Input;
using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class NewPasswordDialogViewModel : ViewModelBase
    {
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly Action _onCancelRequested;
        private readonly Action _onPasswordChangedSuccess;

        public NewPasswordDialogViewModel(Action onCancelRequested, Action onPasswordChangedSuccess)
        {
            _onCancelRequested = onCancelRequested ?? throw new ArgumentNullException(nameof(onCancelRequested));
            _onPasswordChangedSuccess = onPasswordChangedSuccess ?? throw new ArgumentNullException(nameof(onPasswordChangedSuccess));

            ConfirmChangeCommand = new RelayCommand(ConfirmChange);
            CancelCommand = new RelayCommand(_onCancelRequested);
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
            if (string.IsNullOrWhiteSpace(_newPassword) || string.IsNullOrWhiteSpace(_confirmPassword))
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
            _onPasswordChangedSuccess();
        }
    }
}