using System.Collections.Concurrent;

namespace AtariGo.Server.Application.Games;

public sealed partial class GameSessionRegistry
{
    public const int MaximumActionReceipts = 1024;

    private readonly ConcurrentDictionary<long, GameRuntimeEntry> _runtimes = new();

    public void StoreProgress(long gameId, GameRuntimeState progress, long version)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        ArgumentNullException.ThrowIfNull(progress);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!_runtimes.TryGetValue(gameId, out GameRuntimeEntry? entry))
        {
            entry = _runtimes.GetOrAdd(gameId, new GameRuntimeEntry(progress, version, now));
        }
        lock (entry.SyncRoot)
        {
            if (version >= entry.Version)
            {
                entry.Progress = progress;
                entry.Version = version;
                entry.LastActivityUtc = now;
            }
        }
    }

    public IReadOnlyList<long> GetDueGames(DateTime now)
    {
        List<long> due = new List<long>();
        foreach (KeyValuePair<long, GameRuntimeEntry> entry in _runtimes)
        {
            lock (entry.Value.SyncRoot)
            {
                if (!entry.Value.Progress.IsTerminal && entry.Value.Progress.DeadlineUtc <= now)
                {
                    due.Add(entry.Key);
                }
            }
        }
        return due;
    }

    public GameActionResult? FindAction(GameActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_runtimes.TryGetValue(context.Game.GameId, out GameRuntimeEntry? entry))
        {
            return null;
        }
        lock (entry.SyncRoot)
        {
            (long PlayerId, string ActionId) key =
                (context.Player.GamePlayerId, context.Command.ActionId);
            if (!entry.Receipts.TryGetValue(key, out GameActionReceipt? receipt))
            {
                return null;
            }
            if (receipt.Command != context.Command)
            {
                return receipt.Result with { IsAccepted = false, Rejection = "ActionConflict" };
            }
            return receipt.Result;
        }
    }

    public void RememberAction(GameActionContext context, GameActionResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        GameRuntimeEntry entry = _runtimes[context.Game.GameId];
        lock (entry.SyncRoot)
        {
            (long PlayerId, string ActionId) key =
                (context.Player.GamePlayerId, context.Command.ActionId);
            if (!entry.Receipts.ContainsKey(key))
            {
                if (entry.Receipts.Count == MaximumActionReceipts)
                {
                    entry.Receipts.Remove(entry.ReceiptOrder.Dequeue());
                }
                entry.ReceiptOrder.Enqueue(key);
            }
            entry.Receipts[key] = new GameActionReceipt(context.Command, result);
            entry.LastActivityUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }
    }

    private void PruneFinishedRuntimes(DateTime cutoff)
    {
        foreach (KeyValuePair<long, GameRuntimeEntry> entry in _runtimes)
        {
            lock (entry.Value.SyncRoot)
            {
                if (entry.Value.Progress.IsTerminal && entry.Value.LastActivityUtc < cutoff)
                {
                    _runtimes.TryRemove(entry);
                }
            }
        }
    }
}
