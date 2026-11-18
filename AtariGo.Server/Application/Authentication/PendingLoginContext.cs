using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class PendingLoginContext
{
    public PendingLoginContext(User user, PendingLogin pending, AuthenticationCode code)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(code);
        User = user;
        Pending = pending;
        Code = code;
    }

    public User User
    {
        get;
    }

    public PendingLogin Pending
    {
        get; set;
    }

    public AuthenticationCode Code
    {
        get; set;
    }
}
