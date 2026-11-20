using AtariGo.GameLogic.Models;

namespace AtariGo.GameLogic.Actions;

public sealed record MoveResult(GameState State, MoveRejectionReason Rejection)
{
    public bool IsAccepted => Rejection == MoveRejectionReason.None;
}
