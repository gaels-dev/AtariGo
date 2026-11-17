namespace AtariGo.Server.Application.Profile;

public sealed record UserProfileSnapshot(
    string UserName,
    string Email,
    int Wins,
    int AvatarId);
