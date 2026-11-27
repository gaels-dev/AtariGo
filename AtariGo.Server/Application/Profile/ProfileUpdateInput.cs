namespace AtariGo.Server.Application.Profile;

public sealed record ProfileUpdateInput(int UserId, string UserName, int AvatarId);
