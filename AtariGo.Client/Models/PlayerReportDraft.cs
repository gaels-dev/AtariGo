namespace AtariGo.Client.Models;

public sealed record PlayerReportDraft(
    long GameId, PlayerReportCategory Category, string? Description);
