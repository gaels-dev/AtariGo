namespace AtariGo.Server.Application.Games;

internal sealed class GameRuntimeEntry
{
    public GameRuntimeEntry(GameRuntimeState progress, long version, DateTime now)
    {
        Progress = progress;
        Version = version;
        LastActivityUtc = now;
    }

    public object SyncRoot { get; } = new object();

    public GameRuntimeState Progress { get; set; }

    public long Version { get; set; }

    public DateTime LastActivityUtc { get; set; }

    public Dictionary<(long PlayerId, string ActionId), GameActionReceipt> Receipts { get; } =
        new();

    public Queue<(long PlayerId, string ActionId)> ReceiptOrder { get; } = new();
}
