using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Contracts;

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
        return _client.SearchUsersAsync(
            new SearchUsersRequest { Query = query },
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                CreateAuthorizationHeaders())).ResponseAsync;
    }

    public Task<SendFriendRequestResponse> SendFriendRequestAsync(
        int recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return _client.SendFriendRequestAsync(
            new SendFriendRequestRequest
            {
                RecipientUserId = recipientUserId
            },
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                CreateAuthorizationHeaders())).ResponseAsync;
    }

    public Task<GetIncomingFriendRequestsResponse>
        GetIncomingFriendRequestsAsync(
            CancellationToken cancellationToken = default)
    {
        return _client.GetIncomingFriendRequestsAsync(
            new GetIncomingFriendRequestsRequest(),
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                CreateAuthorizationHeaders())).ResponseAsync;
    }

    public Task<RespondToFriendRequestResponse>
        RespondToFriendRequestAsync(
            long requestId,
            bool accept,
            CancellationToken cancellationToken = default)
    {
        return _client.RespondToFriendRequestAsync(
            new RespondToFriendRequestRequest
            {
                RequestId = requestId,
                Accept = accept
            },
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                CreateAuthorizationHeaders())).ResponseAsync;
    }

    public Task<ListFriendRequestsBySenderResponse> ListFriendRequestsBySenderAsync(
        string pageToken,
        CancellationToken cancellationToken = default)
    {
        return _client.ListFriendRequestsBySenderAsync(
            new ListFriendRequestsBySenderRequest
            {
                Filter = FriendRequestFilter.Pending,
                Page = new PageRequest { PageToken = pageToken }
            },
            GrpcCallPolicy.CreateOptions(cancellationToken,
                CreateAuthorizationHeaders())).ResponseAsync;
    }

    public Task<CancelFriendRequestResponse> CancelFriendRequestAsync(
        long requestId,
        CancellationToken cancellationToken = default)
    {
        return _client.CancelFriendRequestAsync(
            new CancelFriendRequestRequest { RequestId = requestId },
            GrpcCallPolicy.CreateOptions(cancellationToken,
                CreateAuthorizationHeaders())).ResponseAsync;
    }

    private Metadata CreateAuthorizationHeaders()
    {
        return new Metadata
        {
            { "authorization", $"Bearer {_sessionToken}" }
        };
    }
}
