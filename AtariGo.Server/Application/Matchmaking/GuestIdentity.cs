namespace AtariGo.Server.Application.Matchmaking;

public sealed record GuestIdentity(
    Guid GuestId,
    string DisplayName,
    DateTimeOffset ExpiresAt);
