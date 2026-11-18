namespace AtariGo.Server.Application.Authentication;

public interface IAuthenticationService
{
    Task<LoginOperationResult> LoginAsync(LoginCommand command,
        CancellationToken cancellationToken);

    Task<LoginOperationResult> VerifyAsync(LoginCodeCommand command,
        CancellationToken cancellationToken);

    Task<LoginOperationResult> ResendAsync(LoginChallengeCommand command,
        CancellationToken cancellationToken);

    Task CancelAsync(string token, CancellationToken cancellationToken);
}
