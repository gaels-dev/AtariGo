namespace AtariGo.Server.Application.Authentication;

public interface IAuthenticationService
{
    /// <summary>Authenticates an account and creates a persisted session.</summary>
    /// <param name="identifier">The account username or email address.</param>
    /// <param name="password">The password supplied by the client.</param>
    /// <param name="cancellationToken">Cancels pending database operations.</param>
    /// <returns>The result of the login operation.</returns>
    Task<LoginOperationResult> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken);
}
