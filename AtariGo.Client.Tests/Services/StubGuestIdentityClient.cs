using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

public sealed class StubGuestIdentityClient : IGuestIdentityClient
{
    public Task<string> Response { get; set; } = Task.FromResult("guest-credential");
    public CancellationToken RequestToken { get; private set; }
    public string? DisplayName { get; private set; }
    public string? RevokedCredential { get; private set; }
    public int Calls { get; private set; }

    public Task<string> CreateAsync(string displayName, CancellationToken cancellationToken)
    {
        RequestToken = cancellationToken;
        DisplayName = displayName;
        Calls++;
        return Response;
    }

    public Task RevokeAsync(string guestCredential, CancellationToken cancellationToken)
    {
        RevokedCredential = guestCredential;
        return Task.CompletedTask;
    }
}
