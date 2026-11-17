using Grpc.Core;

using AtariGo.Contracts;

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
        GetProfileResponse response = await _client.GetProfileAsync(
            new GetProfileRequest(), CreateOptions(cancellationToken)).ResponseAsync;
        return response.Profile;
    }

    public Task<UpdateProfileResponse> UpdateProfileAsync(
        UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _client.UpdateProfileAsync(request, CreateOptions(cancellationToken)).ResponseAsync;
    }

    private CallOptions CreateOptions(CancellationToken cancellationToken)
    {
        Metadata headers = new Metadata
        {
            { "authorization", $"Bearer {_sessionToken}" }
        };
        return GrpcCallPolicy.CreateOptions(cancellationToken, headers);
    }
}
