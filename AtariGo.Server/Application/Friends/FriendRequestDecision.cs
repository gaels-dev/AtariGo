namespace AtariGo.Server.Application.Friends;

internal sealed class FriendRequestDecision
{
    public int RecipientUserId { get; init; }

    public long RequestId { get; init; }

    public bool Accept { get; init; }

    public DateTime RecordedAt { get; init; }
}
