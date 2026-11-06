namespace AtariGo.Server.Application.Games;

public sealed record GameSessionDelivery
{
    public string RecipientPlayerKey { get; init; } = string.Empty;

    public GameSessionSnapshot? Snapshot { get; init; }

    public GameSessionChatMessage? ChatMessage { get; init; }

    public ChatMessageRejectionReason? RejectionReason { get; init; }
}
