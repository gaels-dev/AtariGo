using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public interface IAuthenticationClient
{
    Task<LoginAttemptResult> LoginAsync(string identifier, string password,
        CancellationToken cancellationToken = default);

    Task<LoginAttemptResult> VerifyLoginCodeAsync(string token, string code,
        CancellationToken cancellationToken);

    Task<LoginAttemptResult> ResendLoginCodeAsync(string token,
        CancellationToken cancellationToken);

    Task CancelLoginAsync(string token, CancellationToken cancellationToken);
}
