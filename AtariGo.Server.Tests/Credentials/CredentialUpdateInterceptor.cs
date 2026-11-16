using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AtariGo.Server.Tests.Credentials;

internal sealed class CredentialUpdateInterceptor : DbCommandInterceptor
{
    public Func<Task>? BeforeUpdate { get; set; }

    public bool FailUpdate { get; set; }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("UPDATE", StringComparison.Ordinal)
            && command.CommandText.Contains("[Credentials]", StringComparison.Ordinal))
        {
            Func<Task>? callback = BeforeUpdate;
            BeforeUpdate = null;
            if (callback is not null)
            {
                await callback();
            }
            if (FailUpdate)
            {
                throw new CredentialWriteFailureException();
            }
        }
        return result;
    }
}
