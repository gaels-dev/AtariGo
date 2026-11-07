using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friendships;

namespace AtariGo.Server.Services;

public sealed class FriendshipsGrpcService : Friendships.FriendshipsBase
{
    private readonly IFriendshipsService _friendships;

    public FriendshipsGrpcService(IFriendshipsService friendships)
    {
        ArgumentNullException.ThrowIfNull(friendships);
        _friendships = friendships;
    }

    public override async Task<RemoveFriendshipResponse> RemoveFriendship(
        RemoveFriendshipRequest request,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        RemoveFriendshipResult? result;
        try
        {
            result = await _friendships.RemoveFriendshipAsync(
                token, request.FriendUserId, context.CancellationToken);
        }
        catch (ArgumentException)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "Invalid friend identifier."));
        }

        if (result is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new RemoveFriendshipResponse
        {
            Status = result == RemoveFriendshipResult.Removed
                ? RemoveFriendshipStatus.Removed
                : RemoveFriendshipStatus.AlreadyAbsent
        };
    }
}
