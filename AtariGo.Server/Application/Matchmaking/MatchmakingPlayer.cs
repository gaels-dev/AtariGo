namespace AtariGo.Server.Application.Matchmaking;

public sealed record MatchmakingPlayer(
    string PlayerKey,
    int? UserId,
    string DisplayName,
    string? GuestName);
