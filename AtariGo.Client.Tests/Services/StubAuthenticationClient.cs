using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

public sealed class StubAuthenticationClient : IAuthenticationClient
{
    private readonly Func<CancellationToken, Task<LoginAttemptResult>> _login;

    public StubAuthenticationClient(Func<CancellationToken, Task<LoginAttemptResult>> login)
    {
        ArgumentNullException.ThrowIfNull(login);
        _login = login;
    }

    public Func<CancellationToken, Task<LoginAttemptResult>> Verify
    {
        get; set;
    } =
        _ => Task.FromResult(new LoginAttemptResult { Status = LoginAttemptStatus.InvalidCode });

    public Func<CancellationToken, Task<LoginAttemptResult>> Resend
    {
        get; set;
    } =
        _ => Task.FromResult(new LoginAttemptResult
        {
            Status = LoginAttemptStatus.ChallengeRequired
        });

    public int VerificationCalls
    {
        get; private set;
    }

    public int CancellationCalls
    {
        get; private set;
    }

    public Task<LoginAttemptResult> LoginAsync(string identifier, string password,
        CancellationToken cancellationToken = default)
    {
        return _login(cancellationToken);
    }

    public Task<LoginAttemptResult> VerifyLoginCodeAsync(string token, string code,
        CancellationToken cancellationToken)
    {
        VerificationCalls++;
        return Verify(cancellationToken);
    }

    public Task<LoginAttemptResult> ResendLoginCodeAsync(string token,
        CancellationToken cancellationToken)
    {
        return Resend(cancellationToken);
    }

    public Task CancelLoginAsync(string token, CancellationToken cancellationToken)
    {
        CancellationCalls++;
        return Task.CompletedTask;
    }
}
