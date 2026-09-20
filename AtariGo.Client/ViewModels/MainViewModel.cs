using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel = null!;
        private DialogViewModelBase? _currentDialogViewModel;
        private bool _isGuest;
        private string _playerName;

        public MainViewModel(bool isGuest = false, string playerName = "Player")
        {
            _isGuest = isGuest;
            _playerName = playerName;

            NavigateToMainMenuCommand = new RelayCommand(NavigateToMainMenu);
            NavigateToLobbyCommand = new RelayCommand(NavigateToLobby);
            NavigateToGameBoardCommand = new RelayCommand(() => NavigateToGameBoard(_isGuest, 1));
            CloseDialogCommand = new RelayCommand(CloseDialog);

            NavigateToMainMenu();
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public DialogViewModelBase? CurrentDialogViewModel
        {
            get => _currentDialogViewModel;
            private set
            {
                if (SetProperty(ref _currentDialogViewModel, value))
                {
                    OnPropertyChanged(nameof(IsDialogVisible));
                }
            }
        }

        public bool IsDialogVisible => _currentDialogViewModel != null;

        public bool IsGuest
        {
            get => _isGuest;
            set => SetProperty(ref _isGuest, value);
        }

        public string PlayerName
        {
            get => _playerName;
            set => SetProperty(ref _playerName, value);
        }

        public ICommand NavigateToMainMenuCommand { get; }

        public ICommand NavigateToLobbyCommand { get; }

        public ICommand NavigateToGameBoardCommand { get; }

        public ICommand CloseDialogCommand { get; }

        public void NavigateToMainMenu()
        {
            CurrentViewModel = new MainMenuViewModel(
                onNavigateToLobby: NavigateToLobby,
                onExit: () => System.Windows.Application.Current.Shutdown())
            {
                PlayerName = _playerName,
                IsGuest = _isGuest
            };
        }

        public void NavigateToLobby()
        {
            CurrentViewModel = new LobbyViewModel(
                onStartGame: () => NavigateToGameBoard(_isGuest, 1),
                onCancel: NavigateToMainMenu);
        }

        public void NavigateToGameBoard(bool isGuest = false, int targetCaptures = 1)
        {
            var gameBoard = new GameBoardViewModel(
                onLeaveGame: NavigateToMainMenu,
                onOpenDialog: OpenDialog,
                isGuest: isGuest)
            {
                TargetCaptures = targetCaptures
            };

            CurrentViewModel = gameBoard;
        }

        public void OpenDialog(DialogViewModelBase dialog)
        {
            dialog.DialogClosed += _ => CloseDialog();
            CurrentDialogViewModel = dialog;
        }

        public void CloseDialog()
        {
            CurrentDialogViewModel = null;
        }
    }
}
