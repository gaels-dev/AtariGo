using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

public sealed class StubCredentialsClient : ICredentialsClient
{
    public Task<PasswordChangeResult> Response { get; set; } =
        Task.FromResult(PasswordChangeResult.Changed);

    public CancellationToken RequestToken { get; private set; }

    public int Calls { get; private set; }

    public Task<PasswordChangeResult> ChangePasswordAsync(PasswordChangeInput input,
        CancellationToken cancellationToken)
    {
        Calls++;
        RequestToken = cancellationToken;
        return Response;
    }
}
