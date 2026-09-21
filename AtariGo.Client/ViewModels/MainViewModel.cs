using AtariGo.Client.Commands;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Client.Views;
using System.Globalization;
using System.Threading;
using System.Windows.Input;

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
            NavigateToGameBoardCommand = new RelayCommand(
                () => NavigateToGameBoard(_isGuest, 1));
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

        public event Action? LanguageChanged;

        public ICommand NavigateToMainMenuCommand { get; }

        public ICommand NavigateToLobbyCommand { get; }

        public ICommand NavigateToGameBoardCommand { get; }

        public ICommand CloseDialogCommand { get; }

        public void NavigateToMainMenu()
        {
            CurrentViewModel = new RegisterPlayerLobbyViewModel(
                onNavigateToLobby: NavigateToLobby,
                onOpenOptions: OpenOptionsDialog,
                onExit: () => System.Windows.Application.Current.Shutdown(),
                onSignOut: () =>
                {
                    var loginView = new Views.LoginView();
                    loginView.Show();

                    foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
                    {
                        if (window is MainWindow)
                        {
                            window.Close();
                            break;
                        }
                    }
                })
            {
                PlayerName = _playerName,
                IsGuest = _isGuest
            };
        }

        public void NavigateToLobby()
        {
            CurrentViewModel = new SearchingOpponentViewModel(
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

        public void OpenOptionsDialog()
        {
            var dialog = new OptionsDialogViewModel();
            dialog.DialogClosed += result =>
            {
                if (result == true)
                {
                    ApplyLanguage(dialog.SelectedCultureCode);
                }
            };
            OpenDialog(dialog);
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

        private void ApplyLanguage(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            Properties.Resources.Culture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            CloseDialog();
            LanguageChanged?.Invoke();
        }
    }
}
