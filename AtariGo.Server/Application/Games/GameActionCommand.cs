namespace AtariGo.Server.Application.Games;

public sealed record GameActionCommand
{
    public string ActionId { get; init; } = string.Empty;
    public long ExpectedVersion { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
    public bool IsSurrender { get; init; }
}
