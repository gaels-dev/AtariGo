using System;
using System.Globalization;
using System.Threading;

using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private ViewModelBase _currentViewModel = null!;
        private DialogViewModelBase? _currentDialogViewModel;
        private bool _isGuest;
        private string _playerName;

        public MainViewModel(
            bool isGuest = false,
            string playerName = "Player",
            INavigationService? navigationService = null,
            string? sessionToken = null)
        {
            _isGuest = isGuest;
            _playerName = playerName;
            SessionToken = sessionToken;
            _navigationService =
                navigationService ?? new NavigationService();

            NavigateToMainMenu();
        }

        public ViewModelBase CurrentViewModel
        {
            get
            {
                return _currentViewModel;
            }
            set
            {
                SetProperty(ref _currentViewModel, value);
            }
        }

        public DialogViewModelBase? CurrentDialogViewModel
        {
            get
            {
                return _currentDialogViewModel;
            }
            private set
            {
                if (SetProperty(ref _currentDialogViewModel, value))
                {
                    OnPropertyChanged(nameof(IsDialogVisible));
                }
            }
        }

        public bool IsDialogVisible
        {
            get
            {
                return _currentDialogViewModel != null;
            }
        }

        public bool IsGuest
        {
            get
            {
                return _isGuest;
            }
            set
            {
                SetProperty(ref _isGuest, value);
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

        public string? SessionToken { get; }

        public event Action? LanguageChanged;

        public void NavigateToMainMenu()
        {
            CurrentViewModel = new RegisterPlayerLobbyViewModel(
                _navigationService)
            {
                PlayerName = _playerName,
                IsGuest = _isGuest
            };
        }

        public void NavigateToLobby()
        {
            CurrentViewModel = new SearchingOpponentViewModel(
                _navigationService,
                _isGuest);
        }

        public void NavigateToGameBoard(
            bool isGuest = false,
            int targetCaptures = 1)
        {
            GameBoardViewModel gameBoard =
                new GameBoardViewModel(
                    _navigationService,
                    isGuest: isGuest)
                {
                    TargetCaptures = targetCaptures
                };

            CurrentViewModel = gameBoard;
        }

        public void OpenOptionsDialog()
        {
            OptionsDialogViewModel dialog =
                new OptionsDialogViewModel();

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
            CultureInfo culture = new CultureInfo(cultureCode);

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