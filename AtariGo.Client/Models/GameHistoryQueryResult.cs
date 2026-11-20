namespace AtariGo.Client.Models;

public sealed record GameHistoryQueryResult
{
    public IReadOnlyList<GameHistoryEntry> Entries
    {
        get; init;
    } =
        Array.Empty<GameHistoryEntry>();

    public ClientFailureReason Failure
    {
        get; init;
    }
}
