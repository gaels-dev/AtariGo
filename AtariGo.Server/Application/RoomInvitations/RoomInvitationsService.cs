using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.RoomInvitations;

public sealed class RoomInvitationsService : IRoomInvitationsService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly IRoomsService _rooms;
    private readonly FriendMutationCoordinator _mutations;

    public RoomInvitationsService(
        AtariGoDbContext dbContext,
        IRoomsService rooms,
        FriendMutationCoordinator mutations)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(mutations);
        _dbContext = dbContext;
        _rooms = rooms;
        _mutations = mutations;
    }

    public async Task<RoomInvitationResult> SendAsync(
        SendRoomInvitationCommand command,
        LobbyParticipant sender,
        CancellationToken cancellationToken)
    {
        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        bool areFriends = await AreFriendsAsync(
            sender.UserId, command.RecipientUserId, cancellationToken);
        if (!areFriends)
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.NotFriends);
        }

        LobbyParticipant? recipient = await _dbContext.Users.AsNoTracking()
            .Where(user => user.UserId == command.RecipientUserId
                && user.State == DatabaseValues.AccountStates.Active)
            .Select(user => new LobbyParticipant(user.UserId, user.UserName))
            .SingleOrDefaultAsync(cancellationToken);
        if (recipient is null)
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.RecipientOffline);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _rooms.SendRoomInvitation(command, recipient, sender.UserId);
    }

    public async Task<RoomInvitationResult> RespondAsync(
        RespondRoomInvitationCommand command,
        LobbyParticipant recipient,
        CancellationToken cancellationToken)
    {
        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        RoomInvitationEntry? invitation = _rooms.FindRoomInvitation(
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
        return _rooms.RespondRoomInvitation(command, recipient);
    }

    private Task<bool> AreFriendsAsync(int firstUserId, int secondUserId, CancellationToken token)
    {
        int lowerId = Math.Min(firstUserId, secondUserId);
        int higherId = Math.Max(firstUserId, secondUserId);
        return _dbContext.Friendships.AsNoTracking().AnyAsync(friendship =>
            friendship.UserId == lowerId && friendship.FriendUserId == higherId, token);
    }
}
