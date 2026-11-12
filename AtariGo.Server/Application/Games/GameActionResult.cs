namespace AtariGo.Server.Application.Games;

public sealed record GameActionResult
{
    public string ActionId { get; init; } = string.Empty;
    public bool IsAccepted { get; init; }
    public string Rejection { get; init; } = string.Empty;
    public long Version { get; init; }
}
