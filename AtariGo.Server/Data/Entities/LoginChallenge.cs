namespace AtariGo.Server.Data.Entities;

public sealed class LoginChallenge
{
    public long LoginChallengeId { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public long AuthenticationVersion { get; set; }

    public long? ActiveCodeId { get; set; }

    public string CodeBinding { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime IssueUntil { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? LastSentAt { get; set; }

    public string DeliveryState { get; set; } = "Pending";

    public int DeliveryFailures { get; set; }

    public User User { get; set; } = null!;

    public AuthenticationCode? ActiveCode { get; set; }
}
