

namespace AtariGo.Client.Services;

public interface ISessionClient
{
    Task RunRealtimeAsync(
        SessionRealtimeCallbacks callbacks,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        CancellationToken cancellationToken = default);
}
