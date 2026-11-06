namespace AtariGo.Server.Application.Matchmaking;

public sealed record MatchmakingPlayer
{
    public string PlayerKey { get; init; } = string.Empty;

    public int? UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? GuestName { get; init; }

    public Guid? GuestIdentityId { get; init; }
}
