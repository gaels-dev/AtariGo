using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;
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

    private static RpcException CreateUnavailableException()
    {
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
            var response = new GetProfileResponse { Profile = MapProfile(snapshot) };
            ServerPlayerDiagnostics.Record(_logger, PlayerOperation.ReadProfile,
                new DiagnosticEventDetails
                {
                    Outcome = DiagnosticOutcome.Succeeded,
                    Context = new DiagnosticContext { UserId = userId }
                });
            return response;
        }
        catch (OperationCanceledException exception)
            when (context.CancellationToken.IsCancellationRequested)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.ReadProfile,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (DbException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.ReadProfile,
                (exception, context.CancellationToken));
            throw CreateUnavailableException();
        }
        catch (Exception exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.ReadProfile,
                (exception, context.CancellationToken));
            throw;
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
            ServerPlayerDiagnostics.Record(_logger, PlayerOperation.UpdateProfile,
                PlayerDiagnostics.CreateResult(result.Status) with
                {
                    Context = new DiagnosticContext { UserId = userId }
                });
            return response;
        }
        catch (OperationCanceledException exception)
            when (context.CancellationToken.IsCancellationRequested)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.UpdateProfile,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled, "The request was cancelled."));
        }
        catch (DbUpdateException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.UpdateProfile,
                (exception, context.CancellationToken));
            throw CreateUnavailableException();
        }
        catch (DbException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.UpdateProfile,
                (exception, context.CancellationToken));
            throw CreateUnavailableException();
        }
        catch (Exception exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.UpdateProfile,
                (exception, context.CancellationToken));
            throw;
        }
    }
}
