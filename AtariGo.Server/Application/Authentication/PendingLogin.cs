namespace AtariGo.Server.Application.Authentication;

public sealed record PendingLogin
{
    public int UserId { get; init; }

    public string TokenHash { get; init; } = string.Empty;

    public long CodeId { get; init; }

    public DateTime IssueUntil { get; init; }

    public LoginDeliveryStatus DeliveryStatus { get; init; }

    public int DeliveryFailures { get; init; }

    public bool IsClosed { get; init; }
}
