namespace AtariGo.Diagnostics;

public static class LobbyDiagnostics
{
    public static DiagnosticEvent Create(LobbyOperation operation,
        DiagnosticEventDetails details, bool isClient = false)
    {
        if (operation == LobbyOperation.Unspecified || !Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        var definition = new DiagnosticEventDefinition((int)operation,
            isClient ? "AtariGo.Client.Lobby" : "AtariGo.Server.Lobby", operation.ToString());
        return DiagnosticEventFactory.Create(definition, details);
    }
}
