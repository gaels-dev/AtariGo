using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameActionLookupInterceptor : DbCommandInterceptor
{
    public Action? LookupCallback { get; set; }

    public Func<Task>? LookupAsyncCallback { get; set; }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FROM \"GameEvents\"", StringComparison.Ordinal))
        {
            Action? callback = LookupCallback;
            LookupCallback = null;
            callback?.Invoke();
            Func<Task>? asyncCallback = LookupAsyncCallback;
            LookupAsyncCallback = null;
            if (asyncCallback is not null)
            {
                await asyncCallback();
            }
        }
        return result;
    }
}
