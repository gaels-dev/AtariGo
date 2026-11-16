namespace AtariGo.Server.Application.GameHistory;

public sealed record GameHistoryEntry
{
    public long GameId { get; init; }

    public DateTime PlayedAtUtc { get; init; }

    public string OpponentName { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string StoneColor { get; init; } = string.Empty;

    public string OpponentStoneColor { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public int OpponentCount { get; init; }
}
