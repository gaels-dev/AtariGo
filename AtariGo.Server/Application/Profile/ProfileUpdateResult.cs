namespace AtariGo.Server.Application.Profile;

public sealed record ProfileUpdateResult(
    ProfileUpdateStatus Status,
    UserProfileSnapshot? Profile = null);
