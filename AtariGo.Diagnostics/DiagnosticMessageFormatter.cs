using System.Globalization;
using System.Text;

namespace AtariGo.Diagnostics;

public static class DiagnosticMessageFormatter
{
    private static readonly Dictionary<string, string> messages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ClientStarted"] = "Client diagnostics started.",
            ["ClientStopped"] = "Client diagnostics stopped.",
            ["ClientRequestFailedExceptionType"] = "Client request failed.",
            ["OpenGuestSession"] = "Guest session opening failed.",
            ["ServerListening"] = "Server is listening for connections.",
            ["ApplicationStarted"] = "Application started. Press Ctrl+C to shut down.",
            ["ApplicationStopping"] = "Application is shutting down...",
            ["HostingEnvironmentConfigured"] = "Hosting environment configured.",
            ["ContentRootConfigured"] = "Content root configured.",
            ["KestrelEndpointsOverrideUrls"] =
                "Configured Kestrel endpoints take precedence over hosting URLs.",
            ["HostingUrlsOverrideKestrelEndpoints"] =
                "Hosting URLs take precedence over configured Kestrel endpoints.",
            ["DevelopmentCertificateNotTrusted"] = "Development HTTPS certificate is not trusted.",
            ["DevelopmentCertificatePartiallyTrusted"] =
                "Development HTTPS certificate is only trusted by some clients.",
            ["Http2DisabledWithoutTls"] = "HTTP/2 is disabled because TLS is not enabled."
        };
    private static readonly string[] rpcStatuses =
    [
        "OK", "Cancelled", "Unknown", "InvalidArgument", "DeadlineExceeded", "NotFound",
        "AlreadyExists", "PermissionDenied", "ResourceExhausted", "FailedPrecondition", "Aborted",
        "OutOfRange", "Unimplemented", "Internal", "Unavailable", "DataLoss", "Unauthenticated"
    ];

    public static string Format(DiagnosticEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string operation = entry.Definition.Operation;
        string message = messages.TryGetValue(operation, out string? known)
            ? known : ExpandOperation(operation);
        DiagnosticHostContext? host = entry.Details.Host;
        if (operation == "ServerListening" && host?.ListeningAddress is not null)
        {
            message = "Now listening on: " + host.ListeningAddress;
        }
        if (operation == "HostingEnvironmentConfigured" && host?.EnvironmentName is not null)
        {
            message = "Hosting environment: " + host.EnvironmentName;
        }
        return message + FormatError(entry.Details.Error);
    }

    private static string ExpandOperation(string operation)
    {
        var message = new StringBuilder();
        foreach (char character in operation)
        {
            if (message.Length > 0 && char.IsUpper(character))
            {
                message.Append(' ');
            }
            message.Append(message.Length == 0 ? character : char.ToLowerInvariant(character));
        }
        return message.Append('.').ToString();
    }

    private static string FormatError(DiagnosticError? error)
    {
        if (error is null)
        {
            return string.Empty;
        }
        string message = " Exception type: " + error.TypeName + ".";
        if (error.TypeName == "RpcException" && error.NativeCode is int code)
        {
            string status = code >= 0 && code < rpcStatuses.Length
                ? rpcStatuses[code] : code.ToString(CultureInfo.InvariantCulture);
            message += " RPC status: " + status + ".";
        }
        return message;
    }
}
