using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubReportClient : IReportClient
{
    public PlayerReportStatus ResponseStatus { get; set; } = PlayerReportStatus.Accepted;

    public PlayerReportDraft? ReceivedDraft { get; private set; }

    public int Calls { get; private set; }

    public Task<PlayerReportResult> SubmitReportAsync(PlayerReportDraft draft,
        CancellationToken cancellationToken)
    {
        Calls++;
        ReceivedDraft = draft;
        return Task.FromResult(new PlayerReportResult { Status = ResponseStatus });
    }
}
