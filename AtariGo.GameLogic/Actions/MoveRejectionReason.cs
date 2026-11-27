namespace AtariGo.GameLogic.Actions;

public enum MoveRejectionReason
{
    None,
    Finished,
    NotYourTurn,
    OutOfBounds,
    Occupied,
    Suicide,
    Repetition
}
