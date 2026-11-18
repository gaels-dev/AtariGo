namespace AtariGo.Server.Application.Games;

public enum ChatMessageSubmissionResult
{
    Accepted,
    Empty,
    TooLong,
    Rejected,
    NotConnected
}
