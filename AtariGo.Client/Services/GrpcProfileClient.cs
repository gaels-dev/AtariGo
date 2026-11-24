using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcProfileClient : IProfileClient
{
    private readonly Profile.ProfileClient _client;
    private readonly string _sessionToken;

    public GrpcProfileClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcProfileClient(string sessionToken, CallInvoker callInvoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(callInvoker);
        _sessionToken = sessionToken;
        _client = new Profile.ProfileClient(callInvoker);
    }

    public async Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        GetProfileResponse response = await ClientPlayerDiagnostics.ExecuteAsync(() =>
            _client.GetProfileAsync(new GetProfileRequest(),
                CreateOptions(cancellationToken)).ResponseAsync,
            PlayerOperation.ReadProfile, cancellationToken);
        RecordRead(response.Profile);
        return response.Profile;
    }

    public async Task<UpdateProfileResponse> UpdateProfileAsync(
        UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        UpdateProfileResponse response = await ClientPlayerDiagnostics.ExecuteAsync(() =>
            _client.UpdateProfileAsync(request, CreateOptions(cancellationToken)).ResponseAsync,
            PlayerOperation.UpdateProfile, cancellationToken);
        ClientPlayerDiagnostics.Record(PlayerOperation.UpdateProfile,
            PlayerDiagnostics.CreateResult(response.Status));
        return response;
    }

    private static void RecordRead(UserProfile? profile)
    {
        bool available = profile is not null;
        ClientPlayerDiagnostics.Record(PlayerOperation.ReadProfile, new DiagnosticEventDetails
        {
            Outcome = available ? DiagnosticOutcome.Succeeded : DiagnosticOutcome.Unconfirmed,
            Severity = available ? DiagnosticSeverity.Information : DiagnosticSeverity.Warning
        });
    }

    private CallOptions CreateOptions(CancellationToken cancellationToken)
    {
        Metadata headers = GrpcCallPolicy.CreateHeaders(_sessionToken);
        return GrpcCallPolicy.CreateOptions(cancellationToken, headers);
    }
}
