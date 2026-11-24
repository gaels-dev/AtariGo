using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcFriendClient : IFriendClient
{
    private readonly string _sessionToken;
    private readonly FriendDirectory.FriendDirectoryClient _client;

    public GrpcFriendClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcFriendClient(string sessionToken, CallInvoker callInvoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        ArgumentNullException.ThrowIfNull(callInvoker);

        _sessionToken = sessionToken;
        _client = new FriendDirectory.FriendDirectoryClient(callInvoker);
    }

    public Task<SearchUsersResponse> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return GrpcCallPolicy.ExecuteAsync(() => _client.SearchUsersAsync(
            new SearchUsersRequest { Query = query },
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                GrpcCallPolicy.CreateHeaders(_sessionToken))).ResponseAsync);
    }

    public async Task<SendFriendRequestResponse> SendFriendRequestAsync(
        int recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var request = new SendFriendRequestRequest { RecipientUserId = recipientUserId };
        CallOptions options = GrpcCallPolicy.CreateOptions(cancellationToken,
            GrpcCallPolicy.CreateHeaders(_sessionToken));
        SendFriendRequestResponse response = await GrpcCallPolicy.ExecuteAsync(() =>
            _client.SendFriendRequestAsync(request, options).ResponseAsync);
        ClientIdentityDiagnostics.Completed(IdentityOperation.SendFriendRequest, response.Status);
        return response;
    }

    public Task<GetIncomingFriendRequestsResponse>
        GetIncomingFriendRequestsAsync(
            CancellationToken cancellationToken = default)
    {
        return GrpcCallPolicy.ExecuteAsync(() => _client.GetIncomingFriendRequestsAsync(
            new GetIncomingFriendRequestsRequest(),
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                GrpcCallPolicy.CreateHeaders(_sessionToken))).ResponseAsync);
    }

    public async Task<RespondToFriendRequestResponse>
        RespondToFriendRequestAsync(
            long requestId,
            bool accept,
            CancellationToken cancellationToken = default)
    {
        var request = new RespondToFriendRequestRequest { RequestId = requestId, Accept = accept };
        CallOptions options = GrpcCallPolicy.CreateOptions(cancellationToken,
            GrpcCallPolicy.CreateHeaders(_sessionToken));
        RespondToFriendRequestResponse response = await GrpcCallPolicy.ExecuteAsync(() =>
            _client.RespondToFriendRequestAsync(request, options).ResponseAsync);
        ClientIdentityDiagnostics.Record(IdentityOperation.RespondToFriendRequest,
            IdentityDiagnostics.Result(
                IdentityOperation.RespondToFriendRequest, response.Status) with
            {
                Context = new DiagnosticContext { RequestId = requestId > 0 ? requestId : null }
            });
        return response;
    }

    public Task<ListFriendRequestsBySenderResponse> ListFriendRequestsBySenderAsync(
        string pageToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageToken);
        return GrpcCallPolicy.ExecuteAsync(() => _client.ListFriendRequestsBySenderAsync(
            new ListFriendRequestsBySenderRequest
            {
                Filter = FriendRequestFilter.Pending,
                Page = new PageRequest { PageToken = pageToken }
            },
            GrpcCallPolicy.CreateOptions(cancellationToken,
                GrpcCallPolicy.CreateHeaders(_sessionToken))).ResponseAsync);
    }

    public async Task<CancelFriendRequestResponse> CancelFriendRequestAsync(
        long requestId,
        CancellationToken cancellationToken = default)
    {
        var request = new CancelFriendRequestRequest { RequestId = requestId };
        CallOptions options = GrpcCallPolicy.CreateOptions(cancellationToken,
            GrpcCallPolicy.CreateHeaders(_sessionToken));
        CancelFriendRequestResponse response = await GrpcCallPolicy.ExecuteAsync(() =>
            _client.CancelFriendRequestAsync(request, options).ResponseAsync);
        ClientIdentityDiagnostics.Record(IdentityOperation.CancelFriendRequest,
            IdentityDiagnostics.Result(IdentityOperation.CancelFriendRequest, response.Status) with
            {
                Context = new DiagnosticContext { RequestId = requestId > 0 ? requestId : null }
            });
        return response;
    }
}
