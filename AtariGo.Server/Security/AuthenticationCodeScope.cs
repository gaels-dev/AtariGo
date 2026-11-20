namespace AtariGo.Server.Security;

public sealed class AuthenticationCodeScope
{
    public AuthenticationCodeScope(int userId, string purpose, string binding = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(binding);
        UserId = userId;
        Purpose = purpose;
        Binding = binding;
    }

    public int UserId
    {
        get;
    }

    public string Purpose
    {
        get;
    }

    public string Binding
    {
        get;
    }
}
