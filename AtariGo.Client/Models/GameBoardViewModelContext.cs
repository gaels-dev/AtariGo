using AtariGo.Client.Services;

namespace AtariGo.Client.Models;

public sealed record GameBoardViewModelContext(
    IGameplaySession? Session,
    bool IsGuest,
    string PlayerName)
{
    public IReportClient? ReportClient { get; init; }
}
