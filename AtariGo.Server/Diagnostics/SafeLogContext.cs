using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class SafeLogContext
{
    internal static DiagnosticContext Read<TState>(TState state, DiagnosticContext context)
    {
        if (state is DiagnosticContext supplied)
        {
            return Merge(supplied, context);
        }
        return state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.Aggregate(context, Apply) : context;
    }

    internal static DiagnosticStatus? ReadStatus<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return null;
        }
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (pair.Key is "Status" or "Result" && pair.Value is Enum value
                && Enum.IsDefined(value.GetType(), value))
            {
                return new DiagnosticStatus(value);
            }
        }
        return null;
    }

    private static DiagnosticContext Apply(DiagnosticContext context,
        KeyValuePair<string, object?> pair)
    {
        if (pair.Value is int integer && pair.Key is "GameId" or "RequestId" or "InvitationId")
        {
            pair = new KeyValuePair<string, object?>(pair.Key, (long)integer);
        }
        return pair switch
        {
            { Key: "GameId", Value: long value and > 0 } => context with { GameId = value },
            { Key: "RoomId", Value: int value and > 0 } => context with { RoomId = value },
            { Key: "UserId", Value: int value and > 0 } => context with { UserId = value },
            { Key: "RequestId", Value: long value and > 0 } => context with { RequestId = value },
            { Key: "InvitationId", Value: long value and > 0 }
                => context with
                {
                    InvitationId = value
                },
            { Key: "OperationId", Value: Guid value } => context with { OperationId = value },
            { Key: "ActionId", Value: Guid value } => context with { ActionId = value },
            _ => context
        };
    }

    private static DiagnosticContext Merge(DiagnosticContext value, DiagnosticContext fallback)
    {
        return new DiagnosticContext
        {
            GameId = value.GameId is > 0 ? value.GameId : fallback.GameId,
            RoomId = value.RoomId is > 0 ? value.RoomId : fallback.RoomId,
            UserId = value.UserId is > 0 ? value.UserId : fallback.UserId,
            RequestId = value.RequestId is > 0 ? value.RequestId : fallback.RequestId,
            InvitationId = value.InvitationId is > 0 ? value.InvitationId : fallback.InvitationId,
            ReportId = value.ReportId is > 0 ? value.ReportId : fallback.ReportId,
            OperationId = value.OperationId ?? fallback.OperationId,
            ActionId = value.ActionId ?? fallback.ActionId,
            TraceId = fallback.TraceId ?? value.TraceId
        };
    }
}
