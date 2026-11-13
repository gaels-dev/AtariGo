namespace AtariGo.GameLogic.Models;

public sealed record GameState
{
    public const int BoardSize = 9;
    public StoneColor[] Board { get; init; } = new StoneColor[BoardSize * BoardSize];
    public StoneColor CurrentTurn { get; init; } = StoneColor.Black;
    public int BlackCaptures { get; init; }
    public int WhiteCaptures { get; init; }
    public int CaptureGoal { get; init; } = 1;
    public int TurnNumber { get; init; } = 1;
    public StoneColor Winner { get; init; }
    public BoardPosition? LastMove { get; init; }
    public string[] PositionHistory { get; init; } = [new string('0', BoardSize * BoardSize)];
}
