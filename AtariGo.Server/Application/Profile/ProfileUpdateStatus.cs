namespace AtariGo.Server.Application.Profile;

public enum ProfileUpdateStatus
{
    Updated,
    InvalidUserName,
    UserNameTaken,
    InvalidAvatar,
    AvatarLocked,
    AccountUnavailable
}
