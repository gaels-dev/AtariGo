using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

public sealed class StubPasswordRecoveryClient : IPasswordRecoveryClient
{
    public Task<PasswordRecoveryResult> Response { get; set; } =
        Task.FromResult(PasswordRecoveryResult.Reset);

    public CancellationToken RequestToken { get; private set; }

    public int ResetCalls { get; private set; }

    public Task<PasswordRecoveryResult> RequestAsync(string identifier,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(PasswordRecoveryResult.Accepted);
    }

    public Task<PasswordRecoveryResult> ResetAsync(PasswordRecoveryInput input,
        CancellationToken cancellationToken)
    {
        RequestToken = cancellationToken;
        ResetCalls++;
        return Response;
    }
}
