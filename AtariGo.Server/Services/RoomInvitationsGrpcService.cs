using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Services;

public sealed class RoomInvitationsGrpcService : RoomInvitations.RoomInvitationsBase
{
    private const int InboxRoomId = 0;
    private const int MaximumCultureNameLength = 64;

    private readonly ISessionService _sessions;
    private readonly IRoomInvitationsService _roomInvitationsService;
    private readonly IRoomsService _roomsService;
    private readonly ILogger<RoomInvitationsGrpcService> _logger;

    public RoomInvitationsGrpcService(
        ISessionService sessions,
        IRoomInvitationsService invitations,
        IRoomsService rooms,
        ILogger<RoomInvitationsGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(invitations);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(logger);
        _sessions = sessions;
        _roomInvitationsService = invitations;
        _roomsService = rooms;
        _logger = logger;
    }

    private async Task<LobbyParticipant> AuthenticateAsync(ServerCallContext context)
    {
        SessionIdentity? identity = await _sessions.ValidateSessionAsync(
            SessionRequestAuthentication.GetSessionToken(context), context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new LobbyParticipant(identity.UserId, identity.UserName);
    }

    private static RoomInvitationEntry RequireInvitation(RoomInvitationResult result)
    {
        if (result.Status != RoomInvitationOperationStatus.Success)
        {
            StatusCode code = result.Status switch
            {
                RoomInvitationOperationStatus.NotFound => StatusCode.NotFound,
                RoomInvitationOperationStatus.PermissionDenied => StatusCode.PermissionDenied,
                RoomInvitationOperationStatus.NotFriends => StatusCode.PermissionDenied,
                _ => StatusCode.FailedPrecondition
            };
            throw new RpcException(new Status(code, result.Status.ToString()));
        }

        return result.Invitation
            ?? throw new InvalidOperationException("Invitation result is missing.");
    }

    private static DiagnosticContext InvitationContext(int userId, long invitationId)
    {
        return new DiagnosticContext
        {
            UserId = userId,
            InvitationId = invitationId > 0 ? invitationId : null
        };
    }

    private static RoomInvitation MapInvitation(RoomInvitationEntry entry)
    {
        return new RoomInvitation
        {
            InvitationId = entry.InvitationId,
            RoomId = entry.RoomId,
            RoomCode = entry.RoomCode,
            SenderUserId = entry.Sender.UserId,
            SenderName = entry.Sender.UserName,
            RecipientUserId = entry.Recipient.UserId,
            RecipientName = entry.Recipient.UserName,
            ExpiresAtUnixMilliseconds = entry.ExpiresAt.ToUnixTimeMilliseconds(),
            State = MapState(entry.State)
        };
    }

    private static RoomInvitationState MapState(RoomInvitationStatus invitationStatus)
    {
        return invitationStatus switch
        {
            RoomInvitationStatus.Pending => RoomInvitationState.Pending,
            RoomInvitationStatus.Accepted => RoomInvitationState.Accepted,
            RoomInvitationStatus.Rejected => RoomInvitationState.Rejected,
            RoomInvitationStatus.Cancelled => RoomInvitationState.Cancelled,
            RoomInvitationStatus.Expired => RoomInvitationState.Expired,
            _ => RoomInvitationState.Unavailable
        };
    }

    private static RoomInvitationDeliveryStatus MapDelivery(RoomInvitationResult result)
    {
        return result.Status switch
        {
            RoomInvitationOperationStatus.EmailAccepted =>
                RoomInvitationDeliveryStatus.EmailAccepted,
            RoomInvitationOperationStatus.EmailUnconfirmed =>
                RoomInvitationDeliveryStatus.EmailUnconfirmed,
            RoomInvitationOperationStatus.EmailUnavailable =>
                RoomInvitationDeliveryStatus.EmailUnavailable,
            RoomInvitationOperationStatus.RateLimited => RoomInvitationDeliveryStatus.RateLimited,
            _ => RoomInvitationDeliveryStatus.InApp
        };
    }

    public override async Task<SendRoomInvitationResponse> SendRoomInvitation(
        SendRoomInvitationRequest request, ServerCallContext context)
    {
        LobbyParticipant sender = await AuthenticateAsync(context);
        if (request.RoomId <= 0 || request.RecipientUserId <= 0
            || !Enum.IsDefined(request.DeliveryChannel)
            || request.CultureName.Length > MaximumCultureNameLength)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "InvalidInvitation"));
        }
        var command = new SendRoomInvitationCommand(request.RoomId, request.RecipientUserId)
        {
            SendByEmail = request.DeliveryChannel == RoomInvitationDeliveryChannel.Email,
            CultureName = request.CultureName
        };
        RoomInvitationResult result = await _roomInvitationsService.SendAsync(
            command, sender, context.CancellationToken);
        ServerLobbyDiagnostics.Result(_logger, (LobbyOperation.SendInvitationRequest,
            new DiagnosticContext { RoomId = request.RoomId, UserId = sender.UserId }),
            result.Status);
        var response = new SendRoomInvitationResponse
        {
            DeliveryStatus = MapDelivery(result),
            RetryAfterSeconds = result.RetryAfterSeconds
        };
        if (result.Status == RoomInvitationOperationStatus.Success)
        {
            response.Invitation = MapInvitation(RequireInvitation(result));
        }
        else if (result.Status is RoomInvitationOperationStatus.EmailAccepted
            or RoomInvitationOperationStatus.EmailUnconfirmed
            or RoomInvitationOperationStatus.EmailUnavailable
            or RoomInvitationOperationStatus.RateLimited)
        {
            if (result.Invitation is not null)
            {
                response.Invitation = MapInvitation(result.Invitation);
            }
        }
        else
        {
            RequireInvitation(result);
        }
        return response;
    }

    public override async Task<ListRoomInvitationsResponse> ListRoomInvitations(
        ListRoomInvitationsRequest request, ServerCallContext context)
    {
        LobbyParticipant user = await AuthenticateAsync(context);
        var response = new ListRoomInvitationsResponse();
        int? roomId = request.RoomId == InboxRoomId ? null : request.RoomId;
        response.Invitations.AddRange(_roomsService.ListRoomInvitations(user.UserId, roomId)
            .Select(MapInvitation));
        return response;
    }

    public override async Task<RespondRoomInvitationResponse> RespondRoomInvitation(
        RespondRoomInvitationRequest request, ServerCallContext context)
    {
        LobbyParticipant recipient = await AuthenticateAsync(context);
        var command = new RespondRoomInvitationCommand(request.InvitationId, request.Accept);
        RoomInvitationResult result = await _roomInvitationsService.RespondAsync(
            command, recipient, context.CancellationToken);
        ServerLobbyDiagnostics.Result(_logger, (LobbyOperation.RespondInvitationRequest,
            InvitationContext(recipient.UserId, request.InvitationId)), result.Status);
        var response = new RespondRoomInvitationResponse
        {
            Invitation = MapInvitation(RequireInvitation(result))
        };
        if (result.Room is not null)
        {
            response.Room = RoomsGrpcService.MapRoom(result.Room);
        }

        return response;
    }

    public override async Task<CancelRoomInvitationResponse> CancelRoomInvitation(
        CancelRoomInvitationRequest request, ServerCallContext context)
    {
        LobbyParticipant sender = await AuthenticateAsync(context);
        RoomInvitationResult result = _roomsService.CancelRoomInvitation(
            request.InvitationId, sender.UserId);
        ServerLobbyDiagnostics.Result(_logger, (LobbyOperation.CancelInvitationRequest,
            InvitationContext(sender.UserId, request.InvitationId)), result.Status);
        return new CancelRoomInvitationResponse
        {
            Invitation = MapInvitation(RequireInvitation(result))
        };
    }
}
