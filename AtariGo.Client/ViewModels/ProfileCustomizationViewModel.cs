using System;
using System.Collections.Generic;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class ProfileCustomizationViewModel : ViewModelBase
    {
        private const string DefaultEmail = "player@example.com";
        private const string DefaultAvatar = "🦊";

        private readonly INavigationService _navigationService;
        private string _selectedAvatar;
        private string _playerName;
        private string _email;
        private string _statusMessage;

        public ProfileCustomizationViewModel(
            INavigationService navigationService,
            string playerName)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _navigationService = navigationService;
            _playerName = playerName;
            _email = DefaultEmail;
            _selectedAvatar = DefaultAvatar;
            _statusMessage = string.Empty;

            AvailableAvatars = new List<string>
            {
                "🦊",
                "🤖",
                "👽"
            };

            SelectAvatarCommand = new RelayCommand(SelectAvatar);
            UpdateProfileCommand = new RelayCommand(UpdateProfile);
            ChangePasswordCommand = new RelayCommand(ChangePassword);
            BackCommand = new RelayCommand(
                _navigationService.NavigateToMainMenu);
        }

        public IReadOnlyList<string> AvailableAvatars { get; }

        public string SelectedAvatar
        {
            get
            {
                return _selectedAvatar;
            }
            set
            {
                SetProperty(ref _selectedAvatar, value);
            }
        }

        public string PlayerName
        {
            get
            {
                return _playerName;
            }
            set
            {
                SetProperty(ref _playerName, value);
            }
        }

        public string Email
        {
            get
            {
                return _email;
            }
            set
            {
                SetProperty(ref _email, value);
            }
        }

        public string StatusMessage
        {
            get
            {
                return _statusMessage;
            }
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        public ICommand SelectAvatarCommand { get; }

        public ICommand UpdateProfileCommand { get; }

        public ICommand ChangePasswordCommand { get; }

        public ICommand BackCommand { get; }

        private void SelectAvatar(object? parameter)
        {
            if (parameter is string avatar)
            {
                SelectedAvatar = avatar;
            }
        }

        private void UpdateProfile()
        {
            StatusMessage =
                Properties.Resources.ProfileCustomization_Msg_Updated;
        }

        private void ChangePassword()
        {
            _navigationService.OpenNewPasswordDialog();
        }
    }
}