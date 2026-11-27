using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public interface ICredentialsClient
{
    Task<PasswordChangeResult> ChangePasswordAsync(PasswordChangeInput input,
        CancellationToken cancellationToken);
}
