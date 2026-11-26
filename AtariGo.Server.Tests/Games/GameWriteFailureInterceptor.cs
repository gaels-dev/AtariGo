using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameWriteFailureInterceptor : DbCommandInterceptor
{
    public bool FailMoveWrite
    {
        get; set;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (FailMoveWrite && command.CommandText.Contains("INSERT INTO \"Moves\"",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Simulated move persistence failure.");
        }
        return ValueTask.FromResult(result);
    }
}
