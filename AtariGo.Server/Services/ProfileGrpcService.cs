using System.Data.Common;

using Grpc.Core;
using Microsoft.EntityFrameworkCore;

using AtariGo.Contracts;
using AtariGo.Server.Application.Profile;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class ProfileGrpcService : Profile.ProfileBase
{
    private readonly IProfileService _profileService;
    private readonly ISessionService _sessionService;
    private readonly ISessionConnectionRegistry _connections;
    private readonly ILogger<ProfileGrpcService> _logger;

    public ProfileGrpcService(IProfileService profileService,
        ISessionService sessionService, ISessionConnectionRegistry connections,
        ILogger<ProfileGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(profileService);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(logger);
        _profileService = profileService;
        _sessionService = sessionService;
        _connections = connections;
        _logger = logger;
    }

    public override async Task<GetProfileResponse> GetProfile(
        GetProfileRequest request, ServerCallContext context)
    {
        try
        {
            int userId = await GetAuthenticatedUserIdAsync(context);
            UserProfileSnapshot? snapshot = await _profileService.GetAsync(
                userId, context.CancellationToken);
            if (snapshot is null)
            {
                throw SessionRequestAuthentication.CreateUnauthenticatedException();
            }
            return new GetProfileResponse { Profile = MapProfile(snapshot) };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    public override async Task<UpdateProfileResponse> UpdateProfile(
        UpdateProfileRequest request, ServerCallContext context)
    {
        try
        {
            int userId = await GetAuthenticatedUserIdAsync(context);
            ProfileUpdateInput input = new ProfileUpdateInput(
                userId, request.UserName, request.AvatarId);
            ProfileUpdateResult result = await _profileService.UpdateAsync(
                input, context.CancellationToken);
            if (result.Status == Application.Profile.ProfileUpdateStatus.Updated
                && result.Profile is not null)
            {
                _connections.UpdateUserName(userId, result.Profile.UserName);
            }
            UpdateProfileResponse response = new UpdateProfileResponse
            {
                Status = MapStatus(result.Status)
            };
            if (result.Profile is not null)
            {
                response.Profile = MapProfile(result.Profile);
            }
            return response;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (DbUpdateException exception)
        {
            throw CreateUnavailableException(exception);
        }
        catch (DbException exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    private async Task<int> GetAuthenticatedUserIdAsync(ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            token, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }
        return identity.UserId;
    }

    private RpcException CreateUnavailableException(Exception exception)
    {
        _logger.LogError(exception, "Profile request failed.");
        return new RpcException(new Status(StatusCode.Unavailable,
            "The profile is temporarily unavailable."));
    }

    private static UserProfile MapProfile(UserProfileSnapshot snapshot)
    {
        return new UserProfile
        {
            UserName = snapshot.UserName,
            Email = snapshot.Email,
            Wins = snapshot.Wins,
            AvatarId = snapshot.AvatarId
        };
    }

    private static AtariGo.Contracts.ProfileUpdateStatus MapStatus(
        Application.Profile.ProfileUpdateStatus status)
    {
        return status switch
        {
            Application.Profile.ProfileUpdateStatus.Updated =>
                AtariGo.Contracts.ProfileUpdateStatus.Updated,
            Application.Profile.ProfileUpdateStatus.InvalidUserName =>
                AtariGo.Contracts.ProfileUpdateStatus.InvalidUserName,
            Application.Profile.ProfileUpdateStatus.UserNameTaken =>
                AtariGo.Contracts.ProfileUpdateStatus.UserNameTaken,
            Application.Profile.ProfileUpdateStatus.InvalidAvatar =>
                AtariGo.Contracts.ProfileUpdateStatus.InvalidAvatar,
            Application.Profile.ProfileUpdateStatus.AvatarLocked =>
                AtariGo.Contracts.ProfileUpdateStatus.AvatarLocked,
            Application.Profile.ProfileUpdateStatus.AccountUnavailable =>
                AtariGo.Contracts.ProfileUpdateStatus.AccountUnavailable,
            _ => AtariGo.Contracts.ProfileUpdateStatus.Unspecified
        };
    }
}
