namespace AtariGo.Server.Application.Profile;

public enum ProfileUpdateStatus
{
    Updated,
    InvalidUserName,
    UserNameTaken,
    InvalidAvatar,
    AccountUnavailable
}

public sealed record ProfileUpdateResult(
    ProfileUpdateStatus Status,
    UserProfileSnapshot? Profile = null);
