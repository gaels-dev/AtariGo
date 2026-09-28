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
        private const int ClassicCaptureGoal = 1;
        private const int IntermediateCaptureGoal = 2;
        private const int ExtendedCaptureGoal = 3;
        private const string PrototypeRoomCode = "A7X9";

        private readonly INavigationService _navigationService;
        private int _selectedCaptureGoal;

        public CreatePrivateRoomViewModel(
            INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _navigationService = navigationService;
            _selectedCaptureGoal = ClassicCaptureGoal;

            CaptureGoals = new List<int>
            {
                ClassicCaptureGoal,
                IntermediateCaptureGoal,
                ExtendedCaptureGoal
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