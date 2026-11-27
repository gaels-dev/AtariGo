namespace AtariGo.Server.Application.Reports;

public interface IAccountSanctionService
{
    Task<bool> HasActiveSuspensionAsync(int userId, CancellationToken cancellationToken);
}
