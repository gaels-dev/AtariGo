namespace AtariGo.Server.Application.Rooms;

internal sealed class RoomEntry
{
    public RoomEntry(PrivateRoom snapshot, DateTimeOffset expiresAt)
    {
        Snapshot = snapshot;
        ExpiresAt = expiresAt;
    }

    public PrivateRoom Snapshot { get; private set; }

    public DateTimeOffset ExpiresAt { get; }

    public TaskCompletionSource<PrivateRoom> Changed { get; private set; } =
        CreateSignal();

    public void Publish(PrivateRoom snapshot)
    {
        TaskCompletionSource<PrivateRoom> previousSignal = Changed;
        Snapshot = snapshot;
        Changed = CreateSignal();
        previousSignal.SetResult(snapshot);
    }

    private static TaskCompletionSource<PrivateRoom> CreateSignal()
    {
        return new TaskCompletionSource<PrivateRoom>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
