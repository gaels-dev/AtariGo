using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface ISessionClient
{
    Task<HeartbeatResponse> HeartbeatAsync(
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        CancellationToken cancellationToken = default);
}
