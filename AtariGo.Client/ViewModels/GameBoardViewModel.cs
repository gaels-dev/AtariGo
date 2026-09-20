using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.ViewModels
{
    public class GameBoardViewModel : ViewModelBase
    {
        public const int BoardSize = 9;

        private readonly Action _onLeaveGame;
        private readonly Action<DialogViewModelBase> _onOpenDialog;

        private StoneColor _currentTurn;
        private StoneColor _myColor;
        private string _myPlayerName;
        private string _opponentName;
        private int _blackCaptures;
        private int _whiteCaptures;
        private int _targetCaptures;
        private bool _isChatEnabled;
        private string _chatInput;

        public GameBoardViewModel(
            Action onLeaveGame,
            Action<DialogViewModelBase> onOpenDialog,
            bool isGuest = false)
        {
            _onLeaveGame = onLeaveGame;
            _onOpenDialog = onOpenDialog;
            _targetCaptures = 1;
            _isChatEnabled = !isGuest;

            _currentTurn = StoneColor.Black;
            _myColor = StoneColor.White;
            _myPlayerName = Properties.Resources.GameBoard_Lbl_You;
            _opponentName = Properties.Resources.GameBoard_Lbl_Opponent;
            _chatInput = string.Empty;

            Cells = new ObservableCollection<BoardCellViewModel>();
            ChatMessages = new ObservableCollection<ChatMessageViewModel>();

            InitializeBoard();
            ChatMessages.Add(new ChatMessageViewModel(
                Properties.Resources.GameBoard_Lbl_System,
                Properties.Resources.GameBoard_Chat_GameStarted,
                isSystem: true));
            ChatMessages.Add(new ChatMessageViewModel(
                Properties.Resources.GameBoard_Lbl_Opponent,
                Properties.Resources.GameBoard_Chat_GoodLuck,
                isOpponent: true));

            SendChatMessageCommand = new RelayCommand(
                SendChatMessage,
                () => !string.IsNullOrWhiteSpace(ChatInput));
            SurrenderCommand = new RelayCommand(PromptSurrender);
            ReportPlayerCommand = new RelayCommand(PromptReportPlayer);
            LeaveGameCommand = new RelayCommand(_onLeaveGame);
        }

        public ObservableCollection<BoardCellViewModel> Cells { get; }

        public ObservableCollection<ChatMessageViewModel> ChatMessages { get; }

        public StoneColor CurrentTurn
        {
            get => _currentTurn;
            private set
            {
                if (SetProperty(ref _currentTurn, value))
                {
                    OnPropertyChanged(nameof(IsMyTurn));
                    OnPropertyChanged(nameof(TurnStatusMessage));
                }
            }
        }

        public StoneColor MyColor
        {
            get => _myColor;
            set
            {
                if (SetProperty(ref _myColor, value))
                {
                    OnPropertyChanged(nameof(IsMyTurn));
                    OnPropertyChanged(nameof(TurnStatusMessage));
                    OnPropertyChanged(nameof(MyColorLabel));
                }
            }
        }

        public bool IsMyTurn => _currentTurn == _myColor;

        public string TurnStatusMessage => IsMyTurn
            ? Properties.Resources.GameBoard_Status_YourTurn
            : Properties.Resources.GameBoard_Status_OpponentTurn;

        public string MyColorLabel => _myColor == StoneColor.White
            ? Properties.Resources.GameBoard_Lbl_White
            : Properties.Resources.GameBoard_Lbl_Black;

        public string OpponentColorLabel => _myColor == StoneColor.White
            ? Properties.Resources.GameBoard_Lbl_Black
            : Properties.Resources.GameBoard_Lbl_White;

        public string MyPlayerName
        {
            get => _myPlayerName;
            set => SetProperty(ref _myPlayerName, value);
        }

        public string OpponentName
        {
            get => _opponentName;
            set => SetProperty(ref _opponentName, value);
        }

        public int BlackCaptures
        {
            get => _blackCaptures;
            set => SetProperty(ref _blackCaptures, value);
        }

        public int WhiteCaptures
        {
            get => _whiteCaptures;
            set => SetProperty(ref _whiteCaptures, value);
        }

        public int TargetCaptures
        {
            get => _targetCaptures;
            set => SetProperty(ref _targetCaptures, value);
        }

        public bool IsChatEnabled
        {
            get => _isChatEnabled;
            set => SetProperty(ref _isChatEnabled, value);
        }

        public string ChatInput
        {
            get => _chatInput;
            set => SetProperty(ref _chatInput, value);
        }

        public ICommand SendChatMessageCommand { get; }

        public ICommand SurrenderCommand { get; }

        public ICommand ReportPlayerCommand { get; }

        public ICommand LeaveGameCommand { get; }

        private void InitializeBoard()
        {
            Cells.Clear();
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    Cells.Add(new BoardCellViewModel(row, col, HandleCellClicked));
                }
            }
        }

        private void HandleCellClicked(BoardCellViewModel cell)
        {
            if (cell.Stone != StoneColor.None )//|| !IsMyTurn)
            {
                return;
            }

            foreach (var c in Cells)
            {
                c.IsLastMove = false;
            }

            cell.Stone = CurrentTurn;
            cell.IsLastMove = true;

            CurrentTurn = CurrentTurn == StoneColor.Black ? StoneColor.White : StoneColor.Black;
        }

        private void SendChatMessage()
        {
            if (string.IsNullOrWhiteSpace(ChatInput))
            {
                return;
            }

            ChatMessages.Add(new ChatMessageViewModel(_myPlayerName, ChatInput));
            ChatInput = string.Empty;
        }

        private void PromptSurrender()
        {
            var dialog = new ConfirmActionDialogViewModel(
                "Surrender Match",
                "Are you sure you want to surrender this match?",
                "Surrender");

            dialog.DialogClosed += result =>
            {
                if (result == true)
                {
                    _onLeaveGame();
                }
            };

            _onOpenDialog(dialog);
        }

        private void PromptReportPlayer()
        {
            var dialog = new ReportPlayerDialogViewModel(_opponentName);
            _onOpenDialog(dialog);
        }
    }
}
