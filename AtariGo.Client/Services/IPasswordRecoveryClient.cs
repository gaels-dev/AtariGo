using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public interface IPasswordRecoveryClient
{
    Task<PasswordRecoveryResult> RequestAsync(string identifier,
        CancellationToken cancellationToken);

    Task<PasswordRecoveryResult> ResetAsync(PasswordRecoveryInput input,
        CancellationToken cancellationToken);
}
