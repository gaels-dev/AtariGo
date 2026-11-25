using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class SafeLogHost
{
    internal static DiagnosticHostContext? Read<TState>(TState state, string operation)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return null;
        }
        return operation switch
        {
            "ServerListening" => new DiagnosticHostContext(ReadValue(values, "address"), null),
            "HostingEnvironmentConfigured" =>
                new DiagnosticHostContext(null, ReadValue(values, "EnvName")),
            _ => null
        };
    }

    private static string? ReadValue(IEnumerable<KeyValuePair<string, object?>> values, string key)
    {
        return values.FirstOrDefault(pair => pair.Key == key).Value as string;
    }
}
