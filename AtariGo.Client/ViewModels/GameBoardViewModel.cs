using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class GameBoardViewModel : ViewModelBase, IDisposable
    {
        private const string BlackStoneColorName = "Black";
        private const string WhiteStoneColorName = "White";
        private readonly INavigationService _navigationService;
        private readonly IGameSessionClient? _gameSessionClient;
        private CancellationTokenSource? _sessionCancellation;
        private Task? _sessionTask;

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
        private bool _isDisposed;

        public GameBoardViewModel(
            INavigationService navigationService,
            GameBoardViewModelContext context)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            ArgumentNullException.ThrowIfNull(context);
            _navigationService = navigationService;
            _gameSessionClient = context.SessionClient;
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

            Cells = new ObservableCollection<BoardCellViewModel>();
            ChatMessages = new ObservableCollection<ChatMessageViewModel>();

            InitializeBoard();

            SendChatMessageCommand = new RelayCommand(
                SendChatMessage,
                () => !string.IsNullOrWhiteSpace(ChatInput));
            SurrenderCommand = new RelayCommand(PromptSurrender);
            ReportPlayerCommand = new RelayCommand(PromptReportPlayer);
            LeaveGameCommand = new RelayCommand(_navigationService.NavigateToMainMenu);
        }

        public long? GameId { get; private set; }

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

        public bool IsMyTurn => _currentTurn == _myColor;

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

        public string ChatInput
        {
            get => _chatInput;
            set => SetProperty(ref _chatInput, value);
        }

        public ICommand SendChatMessageCommand { get; }

        public ICommand SurrenderCommand { get; }

        public ICommand ReportPlayerCommand { get; }

        public ICommand LeaveGameCommand { get; }

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
            StartGameSession(room.GameId);
        }

        public void OpenPublicGame(PublicMatchNavigationContext navigationContext)
        {
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
            StartGameSession(game.GameId);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _sessionCancellation?.Cancel();
        }

        private void InitializeBoard()
        {
            Cells.Clear();
            for (int row = 0; row < GameRules.BoardSize; row++)
            {
                for (int col = 0; col < GameRules.BoardSize; col++)
                {
                    Cells.Add(new BoardCellViewModel(row, col, HandleCellClicked));
                }
            }
        }

        private void HandleCellClicked(BoardCellViewModel cell)
        {
            if (GameId is not null || cell.Stone != StoneColor.None)
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
            if (!_isChatEnabled || string.IsNullOrWhiteSpace(ChatInput))
            {
                return;
            }

            ChatMessages.Add(new ChatMessageViewModel(_myPlayerName, ChatInput));
            ChatInput = string.Empty;
        }

        private void PromptSurrender()
        {
            var dialog = new ConfirmActionDialogViewModel(
                Properties.Resources.GameBoard_Surrender_Title,
                Properties.Resources.GameBoard_Surrender_Message,
                Properties.Resources.GameBoard_Surrender_Confirm);

            dialog.DialogClosed += result =>
            {
                if (result == true)
                {
                    _navigationService.NavigateToMainMenu();
                }
            };

            _navigationService.OpenDialog(dialog);
        }

        private void PromptReportPlayer()
        {
            var dialog = new ReportPlayerDialogViewModel(_opponentName);
            _navigationService.OpenDialog(dialog);
        }

        private void StartGameSession(long gameId)
        {
            if (_gameSessionClient is null)
            {
                SessionStatusMessage = GetLocalizedMessage(
                    "GameBoard_Status_ConnectionFailed");
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _sessionCancellation = cancellation;
            SessionStatusMessage = GetLocalizedMessage(
                "GameBoard_Status_Connecting");
            _sessionTask = RunGameSessionAsync(gameId, cancellation);
        }

        private async Task RunGameSessionAsync(
            long gameId,
            CancellationTokenSource cancellation)
        {
            try
            {
                await foreach (GameSessionServerEvent serverEvent in
                    _gameSessionClient!.ConnectAsync(gameId, cancellation.Token))
                {
                    if (serverEvent.EventCase
                        == GameSessionServerEvent.EventOneofCase.Snapshot)
                    {
                        ApplyGameSnapshot(serverEvent.Snapshot);
                    }
                }

                if (!cancellation.IsCancellationRequested)
                {
                    SessionStatusMessage = GetLocalizedMessage(
                        "GameBoard_Status_ConnectionFailed");
                }
            }
            catch (OperationCanceledException)
                when (cancellation.IsCancellationRequested)
            {
            }
            catch (RpcException exception)
                when (GrpcCallPolicy.IsRequestedCancellation(
                    exception,
                    cancellation.Token))
            {
            }
            catch (RpcException exception)
            {
                Trace.TraceWarning(
                    "Game session connection failed with status {0}.",
                    exception.StatusCode);
                SessionStatusMessage = GrpcFailureMessages.GetMessage(exception);
            }
            finally
            {
                if (ReferenceEquals(_sessionCancellation, cancellation))
                {
                    _sessionCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void ApplyGameSnapshot(GameSessionSnapshot snapshot)
        {
            if (!IsValidSnapshot(snapshot))
            {
                SessionStatusMessage = GetLocalizedMessage(
                    "GameBoard_Status_InvalidSnapshot");
                return;
            }

            ApplyPlayerInformation(snapshot);
            ApplyBoardCells(snapshot);

            SessionStatusMessage = GetLocalizedMessage(
                GetConnectionStatusResourceKey(snapshot));
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

        private bool IsValidSnapshot(GameSessionSnapshot snapshot)
        {
            if (snapshot.GameId != GameId
                || snapshot.BoardSize != GameRules.BoardSize
                || snapshot.Cells.Count != Cells.Count
                || !IsValidStoneColor(snapshot.PlayerStoneColor)
                || !IsValidStoneColor(snapshot.OpponentStoneColor))
            {
                return false;
            }

            HashSet<int> cellIndexes = new HashSet<int>();
            foreach (GameBoardCell cell in snapshot.Cells)
            {
                if (cell.Row < 0 || cell.Row >= snapshot.BoardSize
                    || cell.Column < 0 || cell.Column >= snapshot.BoardSize)
                {
                    return false;
                }

                int cellIndex = cell.Row * snapshot.BoardSize + cell.Column;
                if (!cellIndexes.Add(cellIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyPlayerInformation(GameSessionSnapshot snapshot)
        {
            MyPlayerName = snapshot.PlayerName;
            OpponentName = snapshot.OpponentName;
            TargetCaptures = snapshot.CaptureGoal;
            MyColor = MapStoneColor(snapshot.PlayerStoneColor);
            CurrentTurn = StoneColor.Black;
        }

        private void ApplyBoardCells(GameSessionSnapshot snapshot)
        {
            foreach (GameBoardCell cell in snapshot.Cells)
            {
                int cellIndex = cell.Row * snapshot.BoardSize + cell.Column;
                Cells[cellIndex].Stone = MapStone(cell.Stone);
                Cells[cellIndex].IsLastMove = false;
            }
        }

        private static bool IsValidStoneColor(string stoneColor)
        {
            return stoneColor == BlackStoneColorName
                || stoneColor == WhiteStoneColorName;
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
