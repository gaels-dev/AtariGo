using System;
using System.Collections.Generic;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class CreatePrivateRoomViewModel : ViewModelBase, IDisposable
    {
        private const int DefaultGameTimeSeconds = 0;
        private const int DefaultTurnTimeSeconds = 30;

        private readonly INavigationService _navigationService;
        private readonly IRoomsClient _roomsClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private int _selectedCaptureGoal;
        private string _errorMessage = string.Empty;

        public CreatePrivateRoomViewModel(
            INavigationService navigationService,
            IRoomsClient roomsClient)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            ArgumentNullException.ThrowIfNull(roomsClient);
            _navigationService = navigationService;
            _roomsClient = roomsClient;
            _selectedCaptureGoal = GameRules.DefaultCaptureGoal;

            CaptureGoals = new List<int>
            {
                GameRules.DefaultCaptureGoal,
                GameRules.IntermediateCaptureGoal,
                GameRules.ExtendedCaptureGoal
            };

            CreateRoomCommand = new AsyncRelayCommand(
                CreateRoomAsync, ShowFailure, _lifetimeCancellation.Token);
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

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public ICommand CreateRoomCommand { get; }

        public ICommand CancelCommand { get; }

        public void Dispose()
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private async Task CreateRoomAsync(object? parameter)
        {
            CancellationToken cancellationToken = _lifetimeCancellation.Token;
            ErrorMessage = string.Empty;
            var configuration = new RoomConfiguration
            {
                CaptureGoal = SelectedCaptureGoal,
                GameTimeSeconds = DefaultGameTimeSeconds,
                TurnTimeSeconds = DefaultTurnTimeSeconds
            };

            RoomSnapshot room = await _roomsClient.CreatePrivateRoomAsync(
                configuration, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _navigationService.NavigateToPrivateRoomLobby(
                new PrivateRoomNavigationContext(room, IsHost: true));
        }

        private void ShowFailure(Exception exception)
        {
            ErrorMessage = GrpcFailureMessages.GetMessage(exception);
        }
    }
}
