using AtariGo.Server.Application.Authentication;

namespace AtariGo.Server.Tests.Diagnostics;

internal sealed class StubIdentityAuthentication : IAuthenticationService
{
    internal int Calls
    {
        get; private set;
    }

    internal LoginOperationResult Result
    {
        get; init;
    } = new LoginOperationResult(LoginStatus.Success)
    {
        UserId = 7,
        UserName = "private",
        SessionToken = "private",
        MaskedEmail = "private"
    };

    public Task<LoginOperationResult> LoginAsync(LoginCommand command,
        CancellationToken cancellationToken)
    {
        return NextAsync();
    }

    public Task<LoginOperationResult> VerifyAsync(LoginCodeCommand command,
        CancellationToken cancellationToken)
    {
        return NextAsync();
    }

    public Task<LoginOperationResult> ResendAsync(LoginChallengeCommand command,
        CancellationToken cancellationToken)
    {
        return NextAsync();
    }

    public Task CancelAsync(string token, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.CompletedTask;
    }

    private Task<LoginOperationResult> NextAsync()
    {
        Calls++;
        return Task.FromResult(Result);
    }
}
