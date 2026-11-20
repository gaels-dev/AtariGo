using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friendships;

namespace AtariGo.Server.Services;

public sealed class FriendshipsGrpcService : Friendships.FriendshipsBase
{
    private readonly IFriendshipsService _friendships;
    private readonly ILogger<FriendshipsGrpcService> _logger;

    public FriendshipsGrpcService(
        IFriendshipsService friendships,
        ILogger<FriendshipsGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(friendships);
        ArgumentNullException.ThrowIfNull(logger);
        _friendships = friendships;
        _logger = logger;
    }

    private static StatusCode GetDatabaseFailureStatus(DbException? databaseFailure)
    {
        StatusCode status = StatusCode.Internal;
        if (databaseFailure is not null && databaseFailure.IsTransient)
        {
            status = StatusCode.Unavailable;
        }

        return status;
    }

    private RpcException CreatePersistenceFailure(StatusCode status, string errorType)
    {
        _logger.LogError("Friendship removal failed with {ErrorType}: {Status}.",
            errorType, status);
        string message = "The friendship removal could not be completed or confirmed.";
        if (status == StatusCode.Aborted)
        {
            message = "The friendship data changed during the operation.";
        }

        return new RpcException(new Status(status, message));
    }

    public override async Task<RemoveFriendshipResponse> RemoveFriendship(
        RemoveFriendshipRequest removeFriendshipRequest,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        RemoveFriendshipResult? result;
        try
        {
            result = await _friendships.RemoveFriendshipAsync(
                token, removeFriendshipRequest.FriendUserId, context.CancellationToken);
        }
        catch (ArgumentException exception) when (exception.ParamName == "friendUserId")
        {
            _logger.LogWarning("Friendship removal rejected an invalid friend identifier.");
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "Invalid friend identifier."));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw CreatePersistenceFailure(StatusCode.Aborted, exception.GetType().Name);
        }
        catch (DbUpdateException exception)
        {
            throw CreatePersistenceFailure(
                GetDatabaseFailureStatus(exception.InnerException as DbException),
                exception.GetType().Name);
        }
        catch (DbException exception)
        {
            throw CreatePersistenceFailure(
                GetDatabaseFailureStatus(exception), exception.GetType().Name);
        }

        if (result is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        RemoveFriendshipStatus status;
        if (result == RemoveFriendshipResult.Removed)
        {
            status = RemoveFriendshipStatus.Removed;
        }
        else
        {
            status = RemoveFriendshipStatus.AlreadyAbsent;
        }

        return new RemoveFriendshipResponse { Status = status };
    }
}
