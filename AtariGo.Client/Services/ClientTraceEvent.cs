using System.Diagnostics;
using System.Text;

using Grpc.Core;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

internal sealed class ClientTraceEvent
{
    private const int LegacyEventId = 1001;
    private const int MaximumSourceLength = 128;
    private const int MaximumTemplateLength = 512;
    private readonly string _source;
    private readonly TraceEventType _severity;
    private readonly int _id;

    internal ClientTraceEvent(string source, TraceEventType severity, int id)
    {
        _source = IsTechnicalName(source) ? source : "AtariGo.Client.UnknownSource";
        _severity = severity;
        _id = id;
    }

    internal DiagnosticEvent Create(string? format, object?[]? arguments)
    {
        string operation = GetOperation(format, arguments);
        var definition = new DiagnosticEventDefinition(_id > 0 ? _id : LegacyEventId,
            _source, operation);
        var details = new DiagnosticEventDetails
        {
            Severity = GetSeverity(_severity),
            TraceEventId = _id,
            Error = GetError(arguments)
        };
        return new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow);
    }

    private static bool IsTechnicalName(string? name)
    {
        return !string.IsNullOrEmpty(name) && name.Length <= MaximumSourceLength
            && name.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '.' or '_');
    }

    private static DiagnosticError? GetError(object?[]? arguments)
    {
        if (arguments is null)
        {
            return null;
        }
        foreach (object? argument in arguments)
        {
            if (argument is DiagnosticError error)
            {
                return error;
            }
            if (argument is Exception failure)
            {
                return DiagnosticError.FromException(failure);
            }
            if (argument is string name && IsTechnicalName(name))
            {
                Type? type = name == nameof(RpcException)
                    ? typeof(RpcException) : Type.GetType("System." + name);
                if (type is not null && typeof(Exception).IsAssignableFrom(type))
                {
                    return DiagnosticError.FromType(type);
                }
            }
        }
        return null;
    }

    private static string GetOperation(string? format, object?[]? arguments)
    {
        if (format is null || format.Length > MaximumTemplateLength)
        {
            return "UnclassifiedTrace";
        }
        if (arguments is not null)
        {
            try
            {
                CompositeFormat parsed = CompositeFormat.Parse(format);
                if (parsed.MinimumArgumentCount > arguments.Length)
                {
                    return "InvalidTraceFormat";
                }
            }
            catch (FormatException)
            {
                return "InvalidTraceFormat";
            }
        }
        return ClientTraceOperations.Find(format);
    }

    private static DiagnosticSeverity GetSeverity(TraceEventType severity)
    {
        return severity switch
        {
            TraceEventType.Critical => DiagnosticSeverity.Critical,
            TraceEventType.Error => DiagnosticSeverity.Error,
            TraceEventType.Warning => DiagnosticSeverity.Warning,
            TraceEventType.Verbose => DiagnosticSeverity.Debug,
            _ => DiagnosticSeverity.Information
        };
    }
}
