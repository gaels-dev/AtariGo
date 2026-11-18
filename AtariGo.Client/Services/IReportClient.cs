using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public interface IReportClient
{
    Task<PlayerReportResult> SubmitReportAsync(
        PlayerReportDraft draft, CancellationToken cancellationToken);
}
