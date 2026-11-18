namespace AtariGo.Server.Application.Reports;

public sealed class AccountSuspendedException : InvalidOperationException
{
    public AccountSuspendedException()
        : base("A player is temporarily suspended.")
    {
    }
}
