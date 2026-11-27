using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public partial class GameBoardViewModel : ViewModelBase, IDisposable
    {
        private const string BlackStoneColorName = "Black";
        private readonly INavigationService _navigationService;
        private readonly IGameplaySession? _session;
        private readonly IReportClient? _reportClient;
        private readonly bool _isGuest;
        private readonly AsyncRelayCommand _returnToMenuCommand;
        private readonly SynchronizationContext? _uiContext;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private StoneColor _currentTurn;
        private StoneColor _myColor;
        private string _myPlayerName;
        private string _opponentName;
        private int _blackCaptures;
        private int _whiteCaptures;
        private int _targetCaptures;
        private bool _isChatEnabled;
        private string _chatInput;
        private string _sessionStatusMessage;
        private string _chatStatusMessage;
        private int _maximumChatMessageLength;
        private bool _isDisposed;

        public GameBoardViewModel(
            INavigationService navigationService,
            GameBoardViewModelContext context)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            ArgumentNullException.ThrowIfNull(context);
            _navigationService = navigationService;
            _session = context.Session;
            _reportClient = context.ReportClient;
            _isGuest = context.IsGuest;
            _uiContext = SynchronizationContext.Current;
            _targetCaptures = GameRules.DefaultCaptureGoal;
            _isChatEnabled = false;

            _currentTurn = StoneColor.Black;
            _myColor = StoneColor.White;
            _myPlayerName = context.IsGuest
                && !string.IsNullOrWhiteSpace(context.PlayerName)
                    ? context.PlayerName
                    : Properties.Resources.GameBoard_Lbl_You;
            _opponentName = Properties.Resources.GameBoard_Lbl_Opponent;
            _chatInput = string.Empty;
            _sessionStatusMessage = string.Empty;
            _chatStatusMessage = string.Empty;
            _maximumChatMessageLength = 0;
            _returnToMenuCommand = new AsyncRelayCommand(ReturnToMainMenuAsync,
                HandleGameplayFailure, _lifetimeCancellation.Token);

            Cells = new ObservableCollection<BoardCellViewModel>();
            ChatMessages = new ObservableCollection<ChatMessageViewModel>();

            InitializeBoard();
            InitializeGameplay();

            SendChatMessageCommand = new AsyncRelayCommand(
                _ => SendChatMessageAsync(),
                HandleChatSendFailure,
                _lifetimeCancellation.Token);
            SurrenderCommand = new RelayCommand(PromptSurrender);
            ReportPlayerCommand = new RelayCommand(PromptReportPlayer);
            LeaveGameCommand = new RelayCommand(PromptLeaveGame);
            if (_session is not null)
            {
                _session.Updated += HandleSessionUpdate;
            }
        }

        public long? GameId
        {
            get; private set;
        }

        public ObservableCollection<BoardCellViewModel> Cells
        {
            get;
        }

        public ObservableCollection<ChatMessageViewModel> ChatMessages
        {
            get;
        }

        public StoneColor CurrentTurn
        {
            get => _currentTurn;
            private set
            {
                if (SetProperty(ref _currentTurn, value))
                {
                    OnPropertyChanged(nameof(IsMyTurn));
                    OnPropertyChanged(nameof(TurnStatusMessage));
                    OnPropertyChanged(nameof(BoardStatusMessage));
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

        public bool IsMyTurn => !_isDisposed && !_isLeaving && !_isClosingApplication
            && _isPlaying && _currentTurn == _myColor
            && _isSessionConnected && !_hasPendingAction && !_turnPaused;

        public string TurnStatusMessage => IsMyTurn
            ? Properties.Resources.GameBoard_Status_YourTurn
            : Properties.Resources.GameBoard_Status_OpponentTurn;

        public string BoardStatusMessage => GameId.HasValue
            ? SessionStatusMessage
            : TurnStatusMessage;

        public string SessionStatusMessage
        {
            get => _sessionStatusMessage;
            private set
            {
                if (SetProperty(ref _sessionStatusMessage, value))
                {
                    OnPropertyChanged(nameof(BoardStatusMessage));
                }
            }
        }

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

        public string ChatUnavailableMessage => GetLocalizedMessage(
            "GameBoard_Lbl_ChatUnavailable");

        public string ChatStatusMessage
        {
            get => _chatStatusMessage;
            private set => SetProperty(ref _chatStatusMessage, value);
        }

        public int MaximumChatMessageLength
        {
            get => _maximumChatMessageLength;
            private set => SetProperty(ref _maximumChatMessageLength, value);
        }

        public string ChatInput
        {
            get => _chatInput;
            set => SetProperty(ref _chatInput, value);
        }

        public ICommand SendChatMessageCommand
        {
            get;
        }

        public ICommand SurrenderCommand
        {
            get;
        }

        public ICommand ReportPlayerCommand
        {
            get;
        }

        public bool CanReportPlayer => !_isDisposed && !_isGuest
            && _reportClient is not null && GameId.HasValue;

        public ICommand LeaveGameCommand
        {
            get;
        }

        public string GameIdentifier
        {
            get
            {
                string label = string.Empty;
                if (GameId is long gameId)
                {
                    string format = Properties.Resources.ResourceManager.GetString(
                        "GameBoard_Lbl_GameIdentifier", CultureInfo.CurrentUICulture) ?? "#{0}";
                    label = string.Format(CultureInfo.CurrentCulture, format, gameId);
                }

                return label;
            }
        }

        public void OpenSharedGame(PrivateRoomNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationContext);
            RoomSnapshot room = navigationContext.Room;
            GameId = room.GameId;
            TargetCaptures = room.Configuration.CaptureGoal;
            MyColor = StoneColor.White;
            if (navigationContext.IsHost)
            {
                MyColor = StoneColor.Black;
            }

            RoomParticipant? opponent = room.Participants.FirstOrDefault(
                participant => participant.IsHost != navigationContext.IsHost);
            OpponentName = opponent?.UserName ?? Properties.Resources.GameBoard_Lbl_Opponent;
            OnPropertyChanged(nameof(GameIdentifier));
            OnPropertyChanged(nameof(CanReportPlayer));
            StartGameSession(room.GameId);
        }

        public void OpenPublicGame(PublicMatchNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationContext);
            PublicGameStarted game = navigationContext.Game;
            GameId = game.GameId;
            MyPlayerName = navigationContext.PlayerName;
            OpponentName = game.OpponentName;
            TargetCaptures = game.CaptureGoal;
            if (game.StoneColor == BlackStoneColorName)
            {
                MyColor = StoneColor.Black;
            }
            else
            {
                MyColor = StoneColor.White;
            }
            IsChatEnabled = false;
            OnPropertyChanged(nameof(GameIdentifier));
            OnPropertyChanged(nameof(CanReportPlayer));
            StartGameSession(game.GameId);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _applicationExitConfirmation?.TrySetResult(false);
            _clockTimer.Stop();
            _clockTimer.Tick -= OnClockTick;
            _lifetimeCancellation.Cancel();
            if (_session is not null)
            {
                _session.Updated -= HandleSessionUpdate;
                _session.Dispose();
            }
            _lifetimeCancellation.Dispose();
        }

        private void InitializeBoard()
        {
            Cells.Clear();
            for (int row = 0; row < GameRules.BoardSize; row++)
            {
                for (int col = 0; col < GameRules.BoardSize; col++)
                {
                    Cells.Add(new BoardCellViewModel(row, col));
                }
            }
        }

        private async Task SendChatMessageAsync()
        {
            if (!IsChatEnabled
                || _session is null
                || string.IsNullOrWhiteSpace(ChatInput))
            {
                return;
            }

            string message = ChatInput.Trim();
            if (message.Length > MaximumChatMessageLength)
            {
                ChatStatusMessage = GetLocalizedMessage(
                    "GameBoard_Chat_Err_TooLong");
                return;
            }

            bool sent = await _session.SendChatAsync(message, _lifetimeCancellation.Token);
            if (sent && !_isDisposed)
            {
                ChatInput = string.Empty;
                ChatStatusMessage = string.Empty;
            }
        }

        private void PromptSurrender()
        {
            ShowSurrenderConfirmation(false);
        }

        private void ShowSurrenderConfirmation(bool returnToMenu)
        {
            if (!CanSurrender)
            {
                return;
            }
            var dialog = new ConfirmActionDialogViewModel(
                Properties.Resources.GameBoard_Surrender_Title,
                Properties.Resources.GameBoard_Surrender_Message,
                Properties.Resources.GameBoard_Surrender_Confirm);

            dialog.DialogClosed += result => ConfirmSurrender(result, returnToMenu);

            _navigationService.OpenDialog(dialog);
        }

        private void ConfirmSurrender(bool? confirmed, bool returnToMenu)
        {
            if (confirmed != true || _isDisposed)
            {
                return;
            }
            if (returnToMenu && (_isFinished || _hasPermanentSessionFailure))
            {
                ReturnToMainMenu();
            }
            else if (CanSurrender)
            {
                _returnToMenuAfterSurrender = returnToMenu;
                SubmitSurrenderCommand.Execute(null);
            }
        }

        private void PromptReportPlayer()
        {
            if (!CanReportPlayer)
            {
                return;
            }
            ReportPlayerDialogViewModel dialog = new ReportPlayerDialogViewModel(
                _opponentName, GameId!.Value, _reportClient!);
            _navigationService.OpenDialog(dialog);
        }

        private void StartGameSession(long gameId)
        {
            if (_session is null)
            {
                _hasPermanentSessionFailure = true;
                SessionStatusMessage = GetLocalizedMessage(
                    "GameBoard_Status_ConnectionFailed") + " "
                    + GetLocalizedMessage("GameBoard_Connection_Closed");
                NotifyGameplayAvailability();
                ShowClosedConnectionDialog();
                return;
            }
            SessionStatusMessage = GetLocalizedMessage("GameBoard_Status_Connecting");
            _ = _session.RunAsync(gameId, _lifetimeCancellation.Token);
        }

        private void ApplyGameServerEvent(GameplaySessionUpdate update)
        {
            GameSessionServerEvent serverEvent = update.ServerEvent!;
            if (serverEvent.EventCase == GameSessionServerEvent.EventOneofCase.ActionResult)
            {
                if (update.IsMatchingActionResult)
                {
                    ApplyActionResult(serverEvent.ActionResult, update.IsSurrenderResult);
                }
                return;
            }
            if (serverEvent.EventCase
                == GameSessionServerEvent.EventOneofCase.Snapshot)
            {
                ApplyGameSnapshot(serverEvent.Snapshot);
                return;
            }

            if (serverEvent.EventCase
                == GameSessionServerEvent.EventOneofCase.ChatMessage)
            {
                AddChatMessage(serverEvent.ChatMessage);
                return;
            }

            if (serverEvent.EventCase
                == GameSessionServerEvent.EventOneofCase.ChatMessageRejected)
            {
                ShowChatRejection(serverEvent.ChatMessageRejected.Reason);
            }
        }

        private void ApplyGameSnapshot(GameSessionSnapshot snapshot)
        {
            ApplyPlayerInformation(snapshot);
            ApplyBoardCells(snapshot);
            MaximumChatMessageLength = snapshot.MaximumChatMessageLength;
            LoadChatHistory(snapshot);
            IsChatEnabled = !_isGuest;

            ApplyGameplaySnapshot(snapshot);
        }

        private void LoadChatHistory(GameSessionSnapshot snapshot)
        {
            ChatMessages.Clear();
            foreach (GameChatMessage chatMessage in snapshot.ChatHistory)
            {
                AddChatMessage(chatMessage);
            }
        }

        private void AddChatMessage(GameChatMessage chatMessage)
        {
            if (string.IsNullOrWhiteSpace(chatMessage.SenderName)
                || string.IsNullOrWhiteSpace(chatMessage.Text)
                || chatMessage.Text.Length > MaximumChatMessageLength)
            {
                return;
            }

            ChatMessageKind messageKind = chatMessage.IsSender
                ? ChatMessageKind.Player
                : ChatMessageKind.Opponent;
            ChatMessages.Add(new ChatMessageViewModel(
                chatMessage.SenderName,
                chatMessage.Text,
                messageKind));
            ChatStatusMessage = string.Empty;
        }

        private void ShowChatRejection(ChatMessageRejectionReason reason)
        {
            string resourceKey = reason switch
            {
                ChatMessageRejectionReason.Empty => "GameBoard_Chat_Err_Empty",
                ChatMessageRejectionReason.TooLong => "GameBoard_Chat_Err_TooLong",
                ChatMessageRejectionReason.Muted => "GameBoard_Chat_Err_Muted",
                ChatMessageRejectionReason.GuestNotAllowed
                    => "GameBoard_Chat_Err_GuestNotAllowed",
                _ => "GameBoard_Chat_Err_SendFailed"
            };
            ChatStatusMessage = GetLocalizedMessage(resourceKey);
        }

        private void HandleChatSendFailure(Exception exception)
        {
            Trace.TraceWarning(
                "Game chat send failed. Exception type: {0}.",
                exception.GetType().Name);
            ChatStatusMessage = GetLocalizedMessage(
                "GameBoard_Chat_Err_SendFailed");

        }

        private static string GetConnectionStatusResourceKey(
            GameSessionSnapshot snapshot)
        {
            if (snapshot.OpponentConnected)
            {
                return "GameBoard_Status_BothConnected";
            }

            return snapshot.OpponentDisconnected
                ? "GameBoard_Status_OpponentDisconnected"
                : "GameBoard_Status_WaitingOpponent";
        }

        private void ApplyPlayerInformation(GameSessionSnapshot snapshot)
        {
            MyPlayerName = snapshot.PlayerName;
            OpponentName = snapshot.OpponentName;
            TargetCaptures = snapshot.CaptureGoal;
            MyColor = MapStoneColor(snapshot.PlayerStoneColor);
            CurrentTurn = MapStone(snapshot.CurrentTurn);
        }

        private void ApplyBoardCells(GameSessionSnapshot snapshot)
        {
            foreach (GameBoardCell cell in snapshot.Cells)
            {
                int cellIndex = cell.Row * snapshot.BoardSize + cell.Column;
                Cells[cellIndex].Stone = MapStone(cell.Stone);
                Cells[cellIndex].IsLastMove = snapshot.HasLastMove
                    && cell.Row == snapshot.LastMoveRow && cell.Column == snapshot.LastMoveColumn;
            }
        }

        private static StoneColor MapStoneColor(string stoneColor)
        {
            return stoneColor == BlackStoneColorName
                ? StoneColor.Black
                : StoneColor.White;
        }

        private static StoneColor MapStone(GameStoneState stone)
        {
            if (stone == GameStoneState.Black)
            {
                return StoneColor.Black;
            }

            if (stone == GameStoneState.White)
            {
                return StoneColor.White;
            }

            return StoneColor.None;
        }

        private static string GetLocalizedMessage(string resourceKey)
        {
            return Properties.Resources.ResourceManager.GetString(
                resourceKey,
                CultureInfo.CurrentUICulture)
                ?? resourceKey;
        }
    }
}
