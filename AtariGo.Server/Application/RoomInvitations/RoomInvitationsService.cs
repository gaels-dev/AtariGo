using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.RoomInvitations;

public sealed class RoomInvitationsService : IRoomInvitationsService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly IRoomsService _roomsService;
    private readonly FriendMutationCoordinator _mutations;
    private readonly RoomInvitationEmailDeliveryService _emailDelivery;

    public RoomInvitationsService(
        AtariGoDbContext dbContext,
        IRoomsService rooms,
        FriendMutationCoordinator mutations,
        RoomInvitationEmailDeliveryService emailDelivery)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(mutations);
        ArgumentNullException.ThrowIfNull(emailDelivery);
        _dbContext = dbContext;
        _roomsService = rooms;
        _mutations = mutations;
        _emailDelivery = emailDelivery;
    }

    public async Task<RoomInvitationResult> SendAsync(
        SendRoomInvitationCommand command,
        LobbyParticipant sender,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(sender);
        (RoomInvitationResult result, string email) = await PrepareAsync(
            command, sender, cancellationToken);
        if (!command.SendByEmail || result.Invitation is null)
        {
            return result;
        }
        return await _emailDelivery.SendAsync(result.Invitation,
            (email, command.CultureName), cancellationToken);
    }

    public async Task<RoomInvitationResult> RespondAsync(
        RespondRoomInvitationCommand command,
        LobbyParticipant recipient,
        CancellationToken cancellationToken)
    {
        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        RoomInvitationEntry? invitation = _roomsService.FindRoomInvitation(
            command.InvitationId, recipient.UserId);
        if (invitation is null)
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.NotFound);
        }

        if (command.Accept)
        {
            bool areFriends = await AreFriendsAsync(
                invitation.Sender.UserId, recipient.UserId, cancellationToken);
            if (!areFriends)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.NotFriends);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _roomsService.RespondRoomInvitation(command, recipient);
    }

    private async Task<(RoomInvitationResult Result, string Email)> PrepareAsync(
        SendRoomInvitationCommand command,
        LobbyParticipant sender,
        CancellationToken cancellationToken)
    {
        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        bool areFriends = await AreFriendsAsync(
            sender.UserId, command.RecipientUserId, cancellationToken);
        if (!areFriends)
        {
            return (new RoomInvitationResult(RoomInvitationOperationStatus.NotFriends),
                string.Empty);
        }

        var recipient = await _dbContext.Users.AsNoTracking()
            .Where(user => user.UserId == command.RecipientUserId
                && user.State == DatabaseValues.AccountStates.Active)
            .Select(user => new { user.UserId, user.UserName, user.Email })
            .SingleOrDefaultAsync(cancellationToken);
        if (recipient is null)
        {
            return (new RoomInvitationResult(RoomInvitationOperationStatus.RecipientOffline),
                string.Empty);
        }

        cancellationToken.ThrowIfCancellationRequested();
        RoomInvitationResult result = _roomsService.SendRoomInvitation(command,
            new LobbyParticipant(recipient.UserId, recipient.UserName), sender.UserId);
        return (result, recipient.Email);
    }

    private Task<bool> AreFriendsAsync(
        int firstUserId,
        int secondUserId,
        CancellationToken cancellationToken)
    {
        int lowerId = Math.Min(firstUserId, secondUserId);
        int higherId = Math.Max(firstUserId, secondUserId);
        return _dbContext.Friendships.AsNoTracking().AnyAsync(friendship =>
            friendship.UserId == lowerId
            && friendship.FriendUserId == higherId,
            cancellationToken);
    }
}
