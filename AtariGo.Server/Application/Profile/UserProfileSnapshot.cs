namespace AtariGo.Server.Application.Profile;

public sealed record UserProfileSnapshot(
    string UserName,
    string Email)
{
    public required int Wins
    {
        get; init;
    }

    public required int AvatarId
    {
        get; init;
    }
}
