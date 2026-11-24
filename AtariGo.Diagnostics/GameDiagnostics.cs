namespace AtariGo.Diagnostics;

public static class GameDiagnostics
{
    public static DiagnosticEvent Create(GameOperation operation, DiagnosticEventDetails details,
        bool isClient = false)
    {
        if (operation == GameOperation.Unspecified || !Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        var definition = new DiagnosticEventDefinition((int)operation,
            isClient ? "AtariGo.Client.Game" : "AtariGo.Server.Game", operation.ToString());
        return DiagnosticEventFactory.Create(definition, details);
    }

    public static DiagnosticStatus CreateReason(string reason)
    {
        GameDiagnosticReason safe = Enum.TryParse(reason, out GameDiagnosticReason parsed)
            && Enum.IsDefined(parsed) ? parsed : GameDiagnosticReason.Unspecified;
        return new DiagnosticStatus(safe);
    }
}
