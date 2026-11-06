namespace AtariGo.Server.Application.Games;

public sealed record GameSessionChatMessage
{
    public string SenderPlayerKey { get; init; } = string.Empty;

    public string SenderName { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public long SentAtUnixMilliseconds { get; init; }
}
