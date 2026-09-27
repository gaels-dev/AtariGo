using System.Threading;
using System.Threading.Tasks;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IAuthenticationClient
{
    Task<LoginResponse> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default);
}
