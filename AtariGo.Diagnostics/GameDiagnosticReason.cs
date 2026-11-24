namespace AtariGo.Diagnostics;

public enum GameDiagnosticReason
{
    Unspecified,
    Finished,
    Waiting,
    WaitingReconnect,
    StaleVersion,
    InvalidAction,
    ActionConflict,
    NotYourTurn,
    OutOfBounds,
    Occupied,
    Suicide,
    Repetition,
    Surrender,
    CaptureGoal,
    ConnectionTimeout,
    DisconnectionTimeout,
    BothDisconnected
}
