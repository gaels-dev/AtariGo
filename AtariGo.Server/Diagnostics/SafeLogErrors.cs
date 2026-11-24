using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using MailKit.Net.Smtp;

using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class SafeLogErrors
{
    private const int MaximumInnerDepth = 8;

    internal static DiagnosticError? Read<TState>(Exception? exception, TState state)
    {
        Exception? current = exception;
        for (int depth = 0; current is not null && depth < MaximumInnerDepth; depth++)
        {
            if (current is SqlException sql)
            {
                return DiagnosticError.FromException(sql, sql.Number);
            }
            if (current is RpcException rpc)
            {
                return DiagnosticError.FromException(rpc, (int)rpc.StatusCode);
            }
            current = current.InnerException;
        }
        return exception is not null ? DiagnosticError.FromException(exception) : ReadType(state);
    }

    private static DiagnosticError? ReadType<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return null;
        }
        object? value = values.FirstOrDefault(pair => pair.Key == "ErrorType").Value;
        if (value is not string name || !SafeLogNames.IsTechnical(name))
        {
            return null;
        }
        Type? type = ResolveType(name);
        return type is not null && typeof(Exception).IsAssignableFrom(type)
            ? DiagnosticError.FromType(type) : null;
    }

    private static Type? ResolveType(string name)
    {
        return name switch
        {
            nameof(RpcException) => typeof(RpcException),
            nameof(DbUpdateException) => typeof(DbUpdateException),
            nameof(DbUpdateConcurrencyException) => typeof(DbUpdateConcurrencyException),
            nameof(SqlException) => typeof(SqlException),
            nameof(SmtpCommandException) => typeof(SmtpCommandException),
            nameof(SmtpProtocolException) => typeof(SmtpProtocolException),
            nameof(System.Net.Sockets.SocketException) =>
                typeof(System.Net.Sockets.SocketException),
            nameof(System.Security.Authentication.AuthenticationException) =>
                typeof(System.Security.Authentication.AuthenticationException),
            _ => Type.GetType("System." + name)
        };
    }
}
