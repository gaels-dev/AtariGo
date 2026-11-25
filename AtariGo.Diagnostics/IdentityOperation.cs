namespace AtariGo.Diagnostics;

public enum IdentityOperation
{
    Unspecified = 0,
    RegisterAccount = 3001,
    Login = 3002,
    VerifyLoginCode = 3003,
    ResendLoginCode = 3004,
    CancelLogin = 3005,
    RequestPasswordRecovery = 3006,
    ResetPassword = 3007,
    ChangePassword = 3008,
    SendFriendRequest = 3009,
    RespondToFriendRequest = 3010,
    CancelFriendRequest = 3011,
    RemoveFriendship = 3012,
    PresenceConnected = 3013,
    PresenceDisconnected = 3014,
    Logout = 3015,
    SubmitEmail = 3016,
    SubmitLoginEmail = 3017,
    SubmitRecoveryEmail = 3018,
    SubmitInvitationEmail = 3019,
    QueryFriends = 3020,
    RevokeSessions = 3021,
    OpenGuestSession = 3022
}
