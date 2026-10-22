using System;
using System.Collections.Generic;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class CreatePrivateRoomViewModel : ViewModelBase
    {
        private const string PrototypeRoomCode = "A7X9";

        private readonly INavigationService _navigationService;
        private int _selectedCaptureGoal;

        public CreatePrivateRoomViewModel(
            INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _navigationService = navigationService;
            _selectedCaptureGoal = GameRules.DefaultCaptureGoal;

            CaptureGoals = new List<int>
            {
                GameRules.DefaultCaptureGoal,
                GameRules.IntermediateCaptureGoal,
                GameRules.ExtendedCaptureGoal
            };

            CreateRoomCommand = new RelayCommand(CreateRoom);
            CancelCommand = new RelayCommand(
                _navigationService.NavigateToMainMenu);
        }

        public IReadOnlyList<int> CaptureGoals { get; }

        public int SelectedCaptureGoal
        {
            get
            {
                return _selectedCaptureGoal;
            }
            set
            {
                SetProperty(ref _selectedCaptureGoal, value);
            }
        }

        public ICommand CreateRoomCommand { get; }

        public ICommand CancelCommand { get; }

        private void CreateRoom()
        {
            PrivateRoomNavigationContext navigationContext =
                new PrivateRoomNavigationContext
                {
                    IsHost = true,
                    RoomCode = PrototypeRoomCode,
                    TargetCaptures = SelectedCaptureGoal
                };

            _navigationService.NavigateToPrivateRoomLobby(
                navigationContext);
        }
    }
}
