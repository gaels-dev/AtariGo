namespace AtariGo.Diagnostics;

public enum GameOperation
{
    Unspecified = 0,
    Created = 5001,
    Started = 5002,
    Finished = 5003,
    TurnTimedOut = 5004,
    ActionCommitted = 5010,
    ActionRejected = 5011,
    ActionReplayed = 5012,
    ActionConflict = 5013,
    ChatSubmission = 5020,
    ConnectionAttempt = 5030,
    Connected = 5031,
    Disconnected = 5032,
    Restored = 5033,
    StreamFailure = 5034,
    PairedStream = 5035,
    ReconnectExpired = 5036,
    ClockSupervision = 5037,
    Backpressure = 5038
}
