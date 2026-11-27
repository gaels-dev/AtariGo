using AtariGo.Server.Application.Reports;

namespace AtariGo.Server.Tests.Reports;

internal sealed class AllowAllAccountSanctionService : IAccountSanctionService
{
    public bool IsSuspended { get; set; }

    public Task<bool> HasActiveSuspensionAsync(int userId, CancellationToken cancellationToken)
    {
        return Task.FromResult(IsSuspended);
    }
}
