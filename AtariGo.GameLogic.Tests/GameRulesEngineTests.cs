
using AtariGo.GameLogic.Actions;
using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;

namespace AtariGo.GameLogic.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class GameRulesEngineTests
{
    private readonly GameRulesEngine _engine = new GameRulesEngine();

    [TestMethod]
    public void SubmitMove_CornerCapture_ReachesGoalWithoutChangingPreviousState()
    {
        // Arrange
        GameState initial = new GameState();
        GameState first = Play(initial, StoneColor.Black, new BoardPosition(0, 1));
        GameState second = Play(first, StoneColor.White, new BoardPosition(0, 0));
        // Act
        GameState result = Play(second, StoneColor.Black, new BoardPosition(1, 0));
        // Assert
        Assert.AreEqual(StoneColor.Black, result.Winner);
        Assert.AreEqual(1, result.BlackCaptures);
        Assert.AreEqual(StoneColor.Empty, result.Board[0]);
        Assert.AreEqual(StoneColor.White, second.Board[0]);
        Assert.IsTrue(initial.Board.All(stone => stone == StoneColor.Empty));
    }

    [TestMethod]
    public void SubmitMove_SurroundedPoint_RejectsSuicide()
    {
        // Arrange
        GameState state = new GameState { CurrentTurn = StoneColor.White };
        state.Board[1] = StoneColor.Black;
        state.Board[GameState.BoardSize] = StoneColor.Black;
        // Act
        MoveResult result = _engine.SubmitMove(state,
            new MoveCommand(StoneColor.White, new BoardPosition(0, 0)));
        // Assert
        Assert.AreEqual(MoveRejectionReason.Suicide, result.Rejection);
        Assert.AreSame(state, result.State);
    }

    [TestMethod]
    public void SubmitMove_PreviousPosition_RejectsRepetition()
    {
        // Arrange
        string repeated = "1" + new string('0', GameState.BoardSize * GameState.BoardSize - 1);
        GameState state = new GameState { PositionHistory = [repeated] };
        // Act
        MoveResult result = _engine.SubmitMove(state,
            new MoveCommand(StoneColor.Black, new BoardPosition(0, 0)));
        // Assert
        Assert.AreEqual(MoveRejectionReason.Repetition, result.Rejection);
    }

    [TestMethod]
    public void SubmitMove_ConnectedEnemyGroup_CapturesEveryStone()
    {
        // Arrange
        GameState state = new GameState { CaptureGoal = 5 };
        state.Board[0] = StoneColor.White;
        state.Board[1] = StoneColor.White;
        state.Board[GameState.BoardSize] = StoneColor.Black;
        state.Board[GameState.BoardSize + 1] = StoneColor.Black;
        // Act
        GameState next = Play(state, StoneColor.Black, new BoardPosition(0, 2));
        // Assert
        Assert.AreEqual(2, next.BlackCaptures);
        Assert.AreEqual(StoneColor.Empty, next.Board[0]);
        Assert.AreEqual(StoneColor.Empty, next.Board[1]);
        Assert.AreEqual(StoneColor.Empty, next.Winner);
    }

    [TestMethod]
    public void SubmitMove_CaptureCreatesLiberty_AcceptsApparentlySurroundedMove()
    {
        // Arrange
        GameState state = new GameState();
        state.Board[1] = StoneColor.White;
        state.Board[GameState.BoardSize] = StoneColor.White;
        state.Board[2] = StoneColor.Black;
        state.Board[GameState.BoardSize + 1] = StoneColor.Black;
        state.Board[GameState.BoardSize * 2] = StoneColor.Black;
        // Act
        MoveResult result = _engine.SubmitMove(state,
            new MoveCommand(StoneColor.Black, new BoardPosition(0, 0)));
        // Assert
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(2, result.State.BlackCaptures);
    }

    [TestMethod]
    public void SubmitMove_InvalidCoordinatesOrTurn_RejectsWithoutChangingBoard()
    {
        // Arrange
        GameState state = new GameState();
        // Act
        MoveResult bounds = _engine.SubmitMove(state,
            new MoveCommand(StoneColor.Black, new BoardPosition(-1, 0)));
        MoveResult turn = _engine.SubmitMove(state,
            new MoveCommand(StoneColor.White, new BoardPosition(0, 0)));
        // Assert
        Assert.AreEqual(MoveRejectionReason.OutOfBounds, bounds.Rejection);
        Assert.AreEqual(MoveRejectionReason.NotYourTurn, turn.Rejection);
    }

    [TestMethod]
    public void SkipTurn_ActiveGame_ChangesTurnWithoutAddingBoardPosition()
    {
        // Arrange
        GameState state = new GameState();
        // Act
        GameState next = _engine.SkipTurn(state);
        // Assert
        Assert.AreEqual(StoneColor.White, next.CurrentTurn);
        Assert.AreEqual(2, next.TurnNumber);
        Assert.AreSame(state.Board, next.Board);
        Assert.AreEqual(state.PositionHistory.Length, next.PositionHistory.Length);
    }

    private GameState Play(GameState state, StoneColor color, BoardPosition position)
    {
        MoveResult result = _engine.SubmitMove(state, new MoveCommand(color, position));
        Assert.IsTrue(result.IsAccepted, result.Rejection.ToString());
        return result.State;
    }
}
