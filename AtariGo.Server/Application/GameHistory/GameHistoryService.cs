using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.GameHistory;

public sealed class GameHistoryService : IGameHistoryService
{
    public const int MaximumHistoryEntries = 20;
    private const int RequiredOpponentCount = 1;

    private readonly AtariGoDbContext _dbContext;

    public GameHistoryService(AtariGoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GameHistoryEntry>> ListGamesByPlayerAsync(
        SessionIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identity.UserId);
        List<GameHistoryEntry> entries = await _dbContext.GamePlayers
            .AsNoTracking()
            .Where(player => player.UserId == identity.UserId)
            .OrderByDescending(player => player.Game.StartedAt ?? player.JoinedAt)
            .ThenByDescending(player => player.GameId)
            .Take(MaximumHistoryEntries)
            .Select(player => new GameHistoryEntry
            {
                GameId = player.GameId,
                PlayedAtUtc = player.Game.StartedAt ?? player.JoinedAt,
                Result = player.Result,
                State = player.Game.State,
                StoneColor = player.StoneColor,
                OpponentCount = player.Game.GamePlayers.Count(
                    opponent => opponent.GamePlayerId != player.GamePlayerId),
                OpponentName = player.Game.GamePlayers
                    .Where(opponent => opponent.GamePlayerId != player.GamePlayerId)
                    .Select(opponent => opponent.UserId.HasValue
                        ? opponent.User!.UserName : opponent.GuestName)
                    .FirstOrDefault() ?? string.Empty,
                OpponentStoneColor = player.Game.GamePlayers
                    .Where(opponent => opponent.GamePlayerId != player.GamePlayerId)
                    .Select(opponent => opponent.StoneColor)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(cancellationToken);
        foreach (GameHistoryEntry entry in entries)
        {
            ValidateEntry(entry);
        }
        return entries;
    }

    private static void ValidateEntry(GameHistoryEntry entry)
    {
        bool hasValidColors = entry.StoneColor is "Black" or "White"
            && entry.OpponentStoneColor is "Black" or "White"
            && entry.StoneColor != entry.OpponentStoneColor;
        bool hasValidState = entry.State is "Waiting" or "InProgress" or "Completed"
            or "Abandoned" or "Cancelled";
        bool hasValidResult = entry.Result is "Pending" or "Won" or "Lost"
            or "Surrendered" or "Abandoned";
        if (entry.OpponentCount != RequiredOpponentCount
            || string.IsNullOrWhiteSpace(entry.OpponentName)
            || !hasValidColors || !hasValidState || !hasValidResult)
        {
            throw new InvalidOperationException("The game history contains an incomplete summary.");
        }
    }
}
