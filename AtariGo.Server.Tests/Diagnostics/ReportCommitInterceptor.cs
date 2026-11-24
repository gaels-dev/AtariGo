using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AtariGo.Server.Tests.Diagnostics;

internal sealed class ReportCommitInterceptor : DbTransactionInterceptor
{
    internal bool FailCommit
    {
        get; set;
    }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (FailCommit)
        {
            throw new InvalidOperationException("private-commit-failure");
        }
        return ValueTask.FromResult(result);
    }
}
