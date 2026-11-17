using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Profile;

public sealed class ProfileService : IProfileService
{
    private const int DuplicateKeyError = 2601;
    private const int UniqueConstraintError = 2627;

    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ProfileService(AtariGoDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<UserProfileSnapshot?> GetAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        User? user = await _dbContext.Users.AsNoTracking()
            .Include(candidate => candidate.Profile)
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId,
                cancellationToken);
        if (user is null)
        {
            return null;
        }

        return await CreateSnapshotAsync(user, cancellationToken);
    }

    public async Task<ProfileUpdateResult> UpdateAsync(
        ProfileUpdateInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!UserNameRules.IsValid(input.UserName))
        {
            return new ProfileUpdateResult(ProfileUpdateStatus.InvalidUserName);
        }
        if (input.AvatarId < DatabaseValues.AvatarIds.Default
            || input.AvatarId > DatabaseValues.AvatarIds.Maximum)
        {
            return new ProfileUpdateResult(ProfileUpdateStatus.InvalidAvatar);
        }

        User? user = await _dbContext.Users
            .Include(candidate => candidate.Profile)
            .SingleOrDefaultAsync(candidate => candidate.UserId == input.UserId,
                cancellationToken);
        if (user is null)
        {
            return new ProfileUpdateResult(ProfileUpdateStatus.AccountUnavailable);
        }

        string normalizedUserName = input.UserName.Trim();
        bool nameTaken = await _dbContext.Users.AnyAsync(candidate =>
            candidate.UserId != input.UserId && candidate.UserName == normalizedUserName,
            cancellationToken);
        if (nameTaken)
        {
            return new ProfileUpdateResult(ProfileUpdateStatus.UserNameTaken);
        }

        ApplyChanges(user, normalizedUserName, input.AvatarId);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateUserName(exception))
        {
            return new ProfileUpdateResult(ProfileUpdateStatus.UserNameTaken);
        }

        UserProfileSnapshot snapshot = await CreateSnapshotAsync(user, cancellationToken);
        return new ProfileUpdateResult(ProfileUpdateStatus.Updated, snapshot);
    }

    private void ApplyChanges(User user, string userName, int avatarId)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        user.UserName = userName;
        user.LastUpdate = now;
        if (user.Profile is null)
        {
            user.Profile = new AtariGo.Server.Data.Entities.Profile
            {
                AvatarId = avatarId
            };
        }
        user.Profile.AvatarId = avatarId;
        user.Profile.LastUpdate = now;
    }

    private async Task<UserProfileSnapshot> CreateSnapshotAsync(
        User user,
        CancellationToken cancellationToken)
    {
        int wins = await _dbContext.GamePlayers.AsNoTracking().CountAsync(player =>
            player.UserId == user.UserId
            && player.Result == DatabaseValues.GamePlayerResults.Won
            && player.Game.State == DatabaseValues.GameStates.Completed,
            cancellationToken);
        int avatarId = user.Profile?.AvatarId ?? DatabaseValues.AvatarIds.Default;
        return new UserProfileSnapshot(user.UserName, user.Email, wins, avatarId);
    }

    private static bool IsDuplicateUserName(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number == DuplicateKeyError
                || sqlException.Number == UniqueConstraintError)
            && sqlException.Message.Contains("UQ_Users_UserName",
                StringComparison.OrdinalIgnoreCase);
    }
}
