namespace AtariGo.Client.Models;

public sealed record GameHistoryEntry
{
    public long GameId
    {
        get; init;
    }

    public DateTimeOffset PlayedAtUtc
    {
        get; init;
    }

    public string OpponentName { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string StoneColor { get; init; } = string.Empty;

    public string OpponentStoneColor { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;
}
