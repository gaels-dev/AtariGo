using System.Diagnostics;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

internal static class ClientPlayerDiagnostics
{
    internal static async Task<T> ExecuteAsync<T>(Func<Task<T>> action,
        PlayerOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            return await GrpcCallPolicy.ExecuteAsync(action);
        }
        catch (Exception failure) when (GrpcClientFailures.IsTransportFailure(failure))
        {
            RecordFailure(operation, failure, cancellationToken);
            throw;
        }
    }

    internal static void Record(PlayerOperation operation, DiagnosticEventDetails details)
    {
        DiagnosticEvent entry = PlayerDiagnostics.Create(operation, details, isClient: true);
        Trace.TraceInformation("{0}", entry);
    }

    internal static void RecordFailure(PlayerOperation operation, Exception failure,
        CancellationToken cancellationToken)
    {
        Record(operation, ClientLobbyDiagnostics.CreateFailureDetails(failure, cancellationToken));
    }
}
