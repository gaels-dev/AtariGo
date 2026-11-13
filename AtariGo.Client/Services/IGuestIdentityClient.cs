
namespace AtariGo.Client.Services;

public interface IGuestIdentityClient
{
    Task<string> CreateAsync(
        string displayName,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        string guestCredential,
        CancellationToken cancellationToken);
}
