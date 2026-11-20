using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Application.Credentials;

public interface ICredentialsService
{
    Task<ChangePasswordResult> ChangePasswordAsync(SessionIdentity identity,
        ChangePasswordCommand command, CancellationToken cancellationToken);
}
